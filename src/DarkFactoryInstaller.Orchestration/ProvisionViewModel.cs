using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>One row in the live provisioning step list (issue #8, AC2).</summary>
public sealed class StepRow : INotifyPropertyChanged
{
    private ProvisionPhase _phase;
    private string _message = string.Empty;

    public string Step { get; init; } = string.Empty;

    public ProvisionPhase Phase
    {
        get => _phase;
        set { if (_phase != value) { _phase = value; OnChanged(nameof(Phase)); } }
    }

    public string Message
    {
        get => _message;
        set { if (_message != value) { _message = value; OnChanged(nameof(Message)); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// View model for the live provisioning UI (issue #8) extended with per-target state and retry
/// (issue #9). Ingests stdout NDJSON lines and stderr prose, renders a step list keyed by stage, and
/// derives a success/failure banner from the terminal <c>done</c> event. Persists observed progress
/// per target (AC1) so the UI can show the last known progress on relaunch/after failure (AC2) and
/// supports a clean reset (AC3) that tears the cluster down and re-runs. Pure (no WPF dependency) so it
/// is unit-testable with fakes.
/// </summary>
public sealed class ProvisionViewModel : INotifyPropertyChanged
{
    private readonly IStreamingProcessRunner _runner;
    private readonly IProvisionStateStore _store;
    private string? _targetId;
    private string _banner = string.Empty;
    private bool _bannerVisible;
    private bool _bannerIsSuccess;
    private string _log = string.Empty;

    public ObservableCollection<StepRow> Steps { get; } = new();

    public string Banner
    {
        get => _banner;
        private set { if (_banner != value) { _banner = value; OnChanged(nameof(Banner)); } }
    }

    public bool BannerVisible
    {
        get => _bannerVisible;
        private set { if (_bannerVisible != value) { _bannerVisible = value; OnChanged(nameof(BannerVisible)); } }
    }

    public bool BannerIsSuccess
    {
        get => _bannerIsSuccess;
        private set { if (_bannerIsSuccess != value) { _bannerIsSuccess = value; OnChanged(nameof(BannerIsSuccess)); } }
    }

    public string Log
    {
        get => _log;
        private set { if (_log != value) { _log = value; OnChanged(nameof(Log)); } }
    }

    /// <summary>AC4: the store is injected so it can be faked in tests; production uses the file store.</summary>
    public ProvisionViewModel(IStreamingProcessRunner runner, IProvisionStateStore store)
    {
        _runner = runner;
        _store = store;
    }

    /// <summary>Sets the active target and loads its last known progress into the step list (AC2).</summary>
    public void SetTarget(string targetId)
    {
        _targetId = targetId;
        ShowLastProgress();
    }

    /// <summary>AC2: reflect the persisted progress for the current target, if any.</summary>
    public void ShowLastProgress()
    {
        if (_targetId is null) return;
        var state = _store.Load(_targetId);
        Steps.Clear();
        foreach (var step in state.CompletedSteps)
        {
            Steps.Add(new StepRow { Step = step.ToString().ToLowerInvariant(), Phase = ProvisionPhase.Success, Message = "(previous run)" });
        }

        if (state.Outcome == ProvisionOutcome.Succeeded)
        {
            BannerVisible = true;
            BannerIsSuccess = true;
            Banner = "Previous run succeeded.";
        }
        else if (state.Outcome == ProvisionOutcome.Failed)
        {
            BannerVisible = true;
            BannerIsSuccess = false;
            Banner = "Previous run failed. Resume or reset below.";
        }
    }

    /// <summary>AC1: a malformed line is ignored (the parser returns null).</summary>
    public void IngestStdoutLine(string line)
    {
        var ev = new NdjsonEventParser().ParseLine(line);
        if (ev is null) return;
        ApplyEvent(ev);
    }

    /// <summary>AC5 (issue #8): stderr prose is captured to the log view; it never drives status.</summary>
    public void IngestStderrLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        Log += line + "\n";
    }

    private void ApplyEvent(ProvisionEvent ev)
    {
        if (ev.Step == ProvisionStep.Done)
        {
            BannerVisible = true;
            BannerIsSuccess = ev.Phase == ProvisionPhase.Success && (ev.ExitCode is null or 0);
            Banner = BannerIsSuccess ? "Provisioning succeeded." : "Provisioning failed.";
            if (!BannerIsSuccess && ev.Problems.Count > 0)
            {
                Banner += " " + string.Join(" ", ev.Problems.Select(p => ProblemMessageMapper.ToMessage(p.Code)));
            }

            return;
        }

        var key = ev.Step.ToString().ToLowerInvariant();
        var row = Steps.FirstOrDefault(s => s.Step == key);
        if (row is null)
        {
            row = new StepRow { Step = key };
            Steps.Add(row);
        }

        row.Phase = ev.Phase;
        row.Message = ev.Message;
    }

    /// <summary>Runs the up command, streaming the live step list, then persists observed progress (AC1).</summary>
    public async Task RunProvisionAsync(string command, IReadOnlyList<string> args, CancellationToken ct = default)
    {
        Steps.Clear();
        BannerVisible = false;
        Log = string.Empty;
        await _runner.RunAsync(command, args, IngestStdoutLine, IngestStderrLine, ct).ConfigureAwait(false);
        SaveState();
    }

    /// <summary>
    /// AC3: clean re-provision. Tears the cluster down fully (<c>df:sandbox-down --delete-cluster</c>),
    /// clears the persisted progress, then re-runs <c>df:sandbox-up</c> with the same workspace dir so
    /// the CLI's idempotent skip starts from a clean baseline.
    /// </summary>
    public async Task ResetAndProvisionAsync(string targetId, string repo, int issue, string workspaceDir, CancellationToken ct = default)
    {
        _targetId = targetId;
        var (downExe, downArgs) = CliCommands.Down(deleteCluster: true);
        await _runner.RunAsync(downExe, downArgs, IngestStderrLine, IngestStderrLine, ct).ConfigureAwait(false);

        _store.Clear(targetId);
        Steps.Clear();
        BannerVisible = false;

        var (upExe, upArgs) = CliCommands.Up(repo, issue, workspaceDir);
        await _runner.RunAsync(upExe, upArgs, IngestStdoutLine, IngestStderrLine, ct).ConfigureAwait(false);
        SaveState();
    }

    /// <summary>AC1: record the observed completed steps + outcome for the current target.</summary>
    private void SaveState()
    {
        if (_targetId is null) return;

        var completed = Steps
            .Where(s => s.Phase is ProvisionPhase.Success or ProvisionPhase.Skip)
            .Select(s => Enum.TryParse<ProvisionStep>(s.Step, ignoreCase: true, out var st) ? st : (ProvisionStep?)null)
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToHashSet();

        var outcome = BannerVisible && BannerIsSuccess ? ProvisionOutcome.Succeeded
                    : BannerVisible ? ProvisionOutcome.Failed
                    : ProvisionOutcome.InProgress;

        _store.Save(new ProvisionState
        {
            TargetId = _targetId,
            CompletedSteps = completed,
            Outcome = outcome,
            Timestamp = DateTime.UtcNow.ToString("o"),
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

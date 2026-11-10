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
/// View model for the live provisioning UI (issue #8). Ingests stdout NDJSON lines and stderr prose,
/// renders a step list keyed by stage, and derives a success/failure banner from the terminal
/// <c>done</c> event. Pure (no WPF dependency) so it is unit-testable with fakes.
/// </summary>
public sealed class ProvisionViewModel : INotifyPropertyChanged
{
    private readonly IStreamingProcessRunner _runner;
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

    public ProvisionViewModel(IStreamingProcessRunner runner) => _runner = runner;

    /// <summary>AC1: a malformed line is ignored (the parser returns null).</summary>
    public void IngestStdoutLine(string line)
    {
        var ev = new NdjsonEventParser().ParseLine(line);
        if (ev is null) return;
        ApplyEvent(ev);
    }

    /// <summary>AC5: stderr prose is captured to the log view; it never drives status.</summary>
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

    public async Task RunProvisionAsync(string command, IReadOnlyList<string> args, CancellationToken ct = default)
    {
        Steps.Clear();
        BannerVisible = false;
        Log = string.Empty;
        await _runner.RunAsync(command, args, IngestStdoutLine, IngestStderrLine, ct).ConfigureAwait(false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
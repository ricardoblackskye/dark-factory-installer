using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class ProvisionViewModelTests
{
    private sealed class FakeStreamingRunner : IStreamingProcessRunner
    {
        private readonly string[] _stdout;
        private readonly string[] _stderr;
        private readonly int _exitCode;
        public FakeStreamingRunner(string[] stdout, string[] stderr, int exitCode = 0)
        {
            _stdout = stdout;
            _stderr = stderr;
            _exitCode = exitCode;
        }

        public Task<ProcessResult> RunAsync(
            string command,
            IReadOnlyList<string> args,
            Action<string> onStdoutLine,
            Action<string> onStderrLine,
            CancellationToken ct = default)
        {
            foreach (var l in _stdout) onStdoutLine(l);
            foreach (var l in _stderr) onStderrLine(l);
            return Task.FromResult(new ProcessResult(_exitCode, string.Join("\n", _stdout), string.Join("\n", _stderr)));
        }
    }

    /// <summary>In-memory <see cref="IProvisionStateStore"/> for tests (issue #9).</summary>
    private sealed class FakeStore : IProvisionStateStore
    {
        private readonly Dictionary<string, ProvisionState> _map = new();
        public ProvisionState Load(string targetId) => _map.TryGetValue(targetId, out var s) ? s : new ProvisionState { TargetId = targetId };
        public void Save(ProvisionState state) => _map[state.TargetId] = state;
        public void Clear(string targetId) => _map.Remove(targetId);
    }

    /// <summary>Records the commands invoked so reset ordering/flags can be asserted (issue #9, AC3).</summary>
    private sealed class RecordingRunner : IStreamingProcessRunner
    {
        public List<(string Exe, IReadOnlyList<string> Args)> Commands { get; } = new();
        public Task<ProcessResult> RunAsync(
            string command,
            IReadOnlyList<string> args,
            Action<string> onStdoutLine,
            Action<string> onStderrLine,
            CancellationToken ct = default)
        {
            Commands.Add((command, args.ToList()));
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }

    /// <summary>Fake <see cref="IVersionGate"/> for tests (issue #24).</summary>
    private sealed class FakeVersionGate : IVersionGate
    {
        public bool Supported { get; init; } = true;
        public string? DetectedVersion { get; init; } = "0.0.0";
        public IReadOnlyList<string> Errors { get; init; } = System.Array.Empty<string>();
        public Task<VersionGateResult> CheckAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Supported ? VersionGateResult.Ok(DetectedVersion) : VersionGateResult.Unsupported(DetectedVersion, Errors));
    }

    [Fact]
    public void LiveStepListUpdatesFromStream() // AC2 (issue #8)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new[] { string.Empty }, new string[0]), new FakeStore(), new FakeVersionGate());
        vm.IngestStdoutLine(@"{""step"":""check"",""phase"":""start"",""message"":""checking""}");
        vm.IngestStdoutLine(@"{""step"":""check"",""phase"":""success"",""message"":""ok""}");
        vm.IngestStdoutLine(@"{""step"":""vendor"",""phase"":""skip"",""message"":""cached""}");

        Assert.Equal(2, vm.Steps.Count);
        var check = vm.Steps.First(s => s.Step == "check");
        Assert.Equal(ProvisionPhase.Success, check.Phase);
        var vendor = vm.Steps.First(s => s.Step == "vendor");
        Assert.Equal(ProvisionPhase.Skip, vendor.Phase);
    }

    [Fact]
    public void DoneEventSetsSuccessBanner() // AC3 (issue #8)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]), new FakeStore(), new FakeVersionGate());
        vm.IngestStdoutLine(@"{""step"":""done"",""phase"":""success"",""message"":""provisioned"",""exitCode"":0}");

        Assert.True(vm.BannerVisible);
        Assert.True(vm.BannerIsSuccess);
    }

    [Fact]
    public void DoneFailureEventShowsProblemMessage() // AC3 + AC4 (issue #8)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]), new FakeStore(), new FakeVersionGate());
        vm.IngestStdoutLine(@"{""step"":""done"",""phase"":""failure"",""message"":""refused"",""exitCode"":10,""problems"":[{""code"":""prerequisites-missing""}]}");

        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.Contains("A prerequisite is missing", vm.Banner);
    }

    [Fact]
    public void StderrGoesToLogNotStatus() // AC5 (issue #8)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new[] { "some prose warning" }), new FakeStore(), new FakeVersionGate());
        vm.IngestStderrLine("some prose warning");
        Assert.Contains("some prose warning", vm.Log);
    }

    [Fact]
    public void ShowLastProgressPopulatesSteps() // AC2 (issue #9)
    {
        var store = new FakeStore();
        store.Save(new ProvisionState
        {
            TargetId = "t",
            CompletedSteps = new() { ProvisionStep.Check, ProvisionStep.Vendor },
            Outcome = ProvisionOutcome.Succeeded,
        });

        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]), store, new FakeVersionGate());
        vm.SetTarget("t");

        Assert.Equal(2, vm.Steps.Count);
        Assert.True(vm.BannerIsSuccess);
    }

    [Fact]
    public async Task RunSavesStateOnCompletion() // AC1 (issue #9)
    {
        var store = new FakeStore();
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new[]
        {
            @"{""step"":""check"",""phase"":""success"",""message"":""ok""}",
            @"{""step"":""done"",""phase"":""success"",""message"":""provisioned"",""exitCode"":0}",
        }, new string[0]), store, new FakeVersionGate());

        vm.SetTarget("t");
        await vm.RunProvisionAsync("npm", new[] { "run", "df:sandbox-up", "--", "--repo", "x", "--issue", "1" });

        var saved = store.Load("t");
        Assert.Equal(ProvisionOutcome.Succeeded, saved.Outcome);
        Assert.Contains(ProvisionStep.Check, saved.CompletedSteps);
    }

    [Fact]
    public async Task ResetClearsThenReRunsUp() // AC3 (issue #9)
    {
        var store = new FakeStore();
        store.Save(new ProvisionState
        {
            TargetId = "t",
            CompletedSteps = new() { ProvisionStep.Check },
            Outcome = ProvisionOutcome.Succeeded,
        });

        var runner = new RecordingRunner();
        var vm = new ProvisionViewModel(runner, store, new FakeVersionGate());

        await vm.ResetAndProvisionAsync("t", "x", 1, @"C:\wf");

        Assert.Equal(2, runner.Commands.Count);
        Assert.Contains("--delete-cluster", runner.Commands[0].Args); // down first
        Assert.Contains("df:sandbox-up", runner.Commands[1].Args);    // then up
        Assert.Empty(store.Load("t").CompletedSteps);                // prior progress wiped (clean re-provision)
    }

    [Fact]
    public async Task ProcessExitNonZeroNoDoneEvent_ShowsMappedError() // AC1 (issue #10)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0], exitCode: 10), new FakeStore(), new FakeVersionGate());
        await vm.RunProvisionAsync("npm", new[] { "run", "df:sandbox-up" });

        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.Contains("Prerequisites", vm.Banner);
        Assert.Null(vm.Diagnostics); // operator-fixable -> no internal diagnostics
    }

    [Fact]
    public async Task ProcessExitUnknownNoDone_SetsDiagnostics() // AC3/AC5 (issue #10)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0], exitCode: 139), new FakeStore(), new FakeVersionGate());
        await vm.RunProvisionAsync("npm", new[] { "run", "df:sandbox-up" });

        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.NotNull(vm.Diagnostics);
        Assert.Contains("exit=139", vm.Diagnostics);
    }

    [Fact]
    public void DoneFailureWithOperatorFixableProblem_HasNoDiagnostics() // AC3 (issue #10)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]), new FakeStore(), new FakeVersionGate());
        vm.IngestStdoutLine(@"{""step"":""done"",""phase"":""failure"",""message"":""refused"",""exitCode"":10,""problems"":[{""code"":""prerequisites-missing""}]}");

        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.Null(vm.Diagnostics);
    }

    [Fact]
    public void DoneFailureWithInternalProblem_SetsDiagnostics() // AC3 (issue #10)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]), new FakeStore(), new FakeVersionGate());
        vm.IngestStdoutLine(@"{""step"":""done"",""phase"":""failure"",""message"":""boom"",""exitCode"":1,""problems"":[{""code"":""internal""}]}");

        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.Contains("exit=", vm.Diagnostics ?? string.Empty);
    }

    [Fact]
    public async Task VersionGateUnsupported_AbortsBeforeProvision() // issue #24
    {
        var runner = new RecordingRunner();
        var vm = new ProvisionViewModel(runner, new FakeStore(), new FakeVersionGate { Supported = false, DetectedVersion = "0.0.0" });

        await vm.RunProvisionAsync("npm", new[] { "run", "df:sandbox-up" });

        Assert.Empty(runner.Commands); // up never started
        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.Contains("Unsupported agent-eve CLI version", vm.Banner);
    }

    [Fact]
    public async Task VersionGateOk_ProceedsToProvision() // issue #24
    {
        var runner = new RecordingRunner();
        var vm = new ProvisionViewModel(runner, new FakeStore(), new FakeVersionGate());

        await vm.RunProvisionAsync("npm", new[] { "run", "df:sandbox-up" });

        Assert.Single(runner.Commands); // up invoked
        Assert.DoesNotContain("Unsupported", vm.Banner);
    }

    [Fact]
    public async Task ResetVersionGateUnsupported_AbortsBeforeDown() // issue #24
    {
        var runner = new RecordingRunner();
        var vm = new ProvisionViewModel(runner, new FakeStore(), new FakeVersionGate { Supported = false });

        await vm.ResetAndProvisionAsync("t", "x", 1, @"C:\wf");

        Assert.Empty(runner.Commands); // down + up never started
        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
    }
}

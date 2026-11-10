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

    [Fact]
    public void LiveStepListUpdatesFromStream() // AC2 (issue #8)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new[] { string.Empty }, new string[0]), new FakeStore());
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
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]), new FakeStore());
        vm.IngestStdoutLine(@"{""step"":""done"",""phase"":""success"",""message"":""provisioned"",""exitCode"":0}");

        Assert.True(vm.BannerVisible);
        Assert.True(vm.BannerIsSuccess);
    }

    [Fact]
    public void DoneFailureEventShowsProblemMessage() // AC3 + AC4 (issue #8)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]), new FakeStore());
        vm.IngestStdoutLine(@"{""step"":""done"",""phase"":""failure"",""message"":""refused"",""exitCode"":10,""problems"":[{""code"":""prerequisites-missing""}]}");

        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.Contains("A prerequisite is missing", vm.Banner);
    }

    [Fact]
    public void StderrGoesToLogNotStatus() // AC5 (issue #8)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new[] { "some prose warning" }), new FakeStore());
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

        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]), store);
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
        }, new string[0]), store);

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
        var vm = new ProvisionViewModel(runner, store);

        await vm.ResetAndProvisionAsync("t", "x", 1, @"C:\wf");

        Assert.Equal(2, runner.Commands.Count);
        Assert.Contains("--delete-cluster", runner.Commands[0].Args); // down first
        Assert.Contains("df:sandbox-up", runner.Commands[1].Args);    // then up
        Assert.Empty(store.Load("t").CompletedSteps);                // prior progress wiped (clean re-provision)
    }

    [Fact]
    public async Task ProcessExitNonZeroNoDoneEvent_ShowsMappedError() // AC1 (issue #10)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0], exitCode: 10), new FakeStore());
        await vm.RunProvisionAsync("npm", new[] { "run", "df:sandbox-up" });

        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.Contains("Prerequisites", vm.Banner);
        Assert.Null(vm.Diagnostics); // operator-fixable -> no internal diagnostics
    }

    [Fact]
    public async Task ProcessExitUnknownNoDone_SetsDiagnostics() // AC3/AC5 (issue #10)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0], exitCode: 139), new FakeStore());
        await vm.RunProvisionAsync("npm", new[] { "run", "df:sandbox-up" });

        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.NotNull(vm.Diagnostics);
        Assert.Contains("exit=139", vm.Diagnostics);
    }

    [Fact]
    public void DoneFailureWithOperatorFixableProblem_HasNoDiagnostics() // AC3 (issue #10)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]), new FakeStore());
        vm.IngestStdoutLine(@"{""step"":""done"",""phase"":""failure"",""message"":""refused"",""exitCode"":10,""problems"":[{""code"":""prerequisites-missing""}]}");

        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.Null(vm.Diagnostics);
    }

    [Fact]
    public void DoneFailureWithInternalProblem_SetsDiagnostics() // AC3 (issue #10)
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]), new FakeStore());
        vm.IngestStdoutLine(@"{""step"":""done"",""phase"":""failure"",""message"":""boom"",""exitCode"":1,""problems"":[{""code"":""internal""}]}");

        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.Contains("exit=", vm.Diagnostics ?? string.Empty);
    }
}

using System.Collections.Generic;
using System.Linq;
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

        public FakeStreamingRunner(string[] stdout, string[] stderr)
        {
            _stdout = stdout;
            _stderr = stderr;
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
            return Task.FromResult(new ProcessResult(0, string.Join("\n", _stdout), string.Join("\n", _stderr)));
        }
    }

    [Fact]
    public void LiveStepListUpdatesFromStream() // AC2
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new[] { string.Empty }, new string[0]));
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
    public void DoneEventSetsSuccessBanner() // AC3
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]));
        vm.IngestStdoutLine(@"{""step"":""done"",""phase"":""success"",""message"":""provisioned"",""exitCode"":0}");

        Assert.True(vm.BannerVisible);
        Assert.True(vm.BannerIsSuccess);
    }

    [Fact]
    public void DoneFailureEventShowsProblemMessage() // AC3 + AC4
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new string[0]));
        vm.IngestStdoutLine(@"{""step"":""done"",""phase"":""failure"",""message"":""refused"",""exitCode"":10,""problems"":[{""code"":""prerequisites-missing""}]}");

        Assert.True(vm.BannerVisible);
        Assert.False(vm.BannerIsSuccess);
        Assert.Contains("A prerequisite is missing", vm.Banner);
    }

    [Fact]
    public void StderrGoesToLogNotStatus() // AC5
    {
        var vm = new ProvisionViewModel(new FakeStreamingRunner(new string[0], new[] { "some prose warning" }));
        vm.IngestStderrLine("some prose warning");
        Assert.Contains("some prose warning", vm.Log);
    }
}
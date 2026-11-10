using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class CliVersionGateTests
{
    private sealed class FakeProcessRunner : IProcessRunner
    {
        public string? Command { get; private set; }
        public IReadOnlyList<string>? Args { get; private set; }
        public int ExitCode { get; init; }
        public string Stdout { get; init; } = string.Empty;
        public string Stderr { get; init; } = string.Empty;

        public Task<ProcessResult> RunAsync(string command, IReadOnlyList<string> args, CancellationToken cancellationToken = default)
        {
            Command = command;
            Args = args;
            return Task.FromResult(new ProcessResult(ExitCode, Stdout, Stderr));
        }
    }

    [Fact]
    public async Task EmptyMinimum_ShortCircuitsToOk_WithoutInvokingCli()
    {
        var runner = new FakeProcessRunner();
        var gate = new CliVersionGate(runner, string.Empty);

        var result = await gate.CheckAsync();

        Assert.True(result.Supported);
        Assert.Null(result.DetectedVersion);
        Assert.Null(runner.Command); // never invoked
    }

    [Fact]
    public async Task SupportedVersion_ReturnsOk()
    {
        var runner = new FakeProcessRunner { Stdout = "1.2.3\n" };
        var gate = new CliVersionGate(runner, ">=1.0.0");

        var result = await gate.CheckAsync();

        Assert.True(result.Supported);
        Assert.Equal("1.2.3", result.DetectedVersion);
    }

    [Fact]
    public async Task UnsupportedVersion_ReturnsUnsupported_WithDetected()
    {
        var runner = new FakeProcessRunner { Stdout = "1.2.3\n" };
        var gate = new CliVersionGate(runner, ">=2.0.0");

        var result = await gate.CheckAsync();

        Assert.False(result.Supported);
        Assert.Equal("1.2.3", result.DetectedVersion);
    }

    [Fact]
    public async Task UnparseableVersion_ReturnsUnsupported()
    {
        var runner = new FakeProcessRunner { Stdout = "no version here" };
        var gate = new CliVersionGate(runner, ">=0.0.0");

        var result = await gate.CheckAsync();

        Assert.False(result.Supported);
        Assert.Null(result.DetectedVersion);
    }

    [Fact]
    public async Task InvokesNpmRunSandboxUpVersion()
    {
        var runner = new FakeProcessRunner { Stdout = "0.0.0\n" };
        var gate = new CliVersionGate(runner, ">=0.0.0");

        await gate.CheckAsync();

        Assert.Equal("npm", runner.Command);
        Assert.Equal(new[] { "run", "df:sandbox-up", "--", "--version" }, runner.Args);
    }
}

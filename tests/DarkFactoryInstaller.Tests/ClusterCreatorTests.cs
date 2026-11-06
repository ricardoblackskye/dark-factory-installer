using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

/// <summary>Fake <see cref="IProcessRunner"/> that returns a scripted result and records the call.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public string? LastCommand { get; private set; }
    public IReadOnlyList<string>? LastArgs { get; private set; }
    public int CallCount { get; private set; }

    public ProcessResult NextResult { get; set; } = new(0, string.Empty, string.Empty);
    public System.Exception? ThrowOnRun { get; set; }

    public Task<ProcessResult> RunAsync(string command, IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        LastCommand = command;
        LastArgs = args;
        CallCount++;
        if (ThrowOnRun is not null)
        {
            throw ThrowOnRun;
        }

        return Task.FromResult(NextResult);
    }
}

public class ClusterCreatorTests
{
    private static SandboxRunRequest SampleRequest()
    {
        SandboxRunRequest.TryCreate("owner/repo", 1, out var req, out _);
        return req!;
    }

    [Fact]
    public async Task AC1_CreateAsync_RunsMinikubeStart_AndCapturesKubeContext()
    {
        var fake = new FakeProcessRunner
        {
            NextResult = new ProcessResult(0, "Done. * kubectl context will be \"darkfactory\"", string.Empty),
        };
        IClusterCreator creator = new MinikubeClusterCreator(fake);

        var result = await creator.CreateAsync("darkfactory");

        Assert.True(result.Success);
        Assert.Equal("darkfactory", result.Profile);
        Assert.Equal("darkfactory", result.KubeContext);
        Assert.Equal(ClusterState.Created, result.State);
        Assert.Equal("minikube", fake.LastCommand);
        Assert.Equal(3, fake.LastArgs!.Count);
        Assert.Equal("start", fake.LastArgs[0]);
        Assert.Equal("-p", fake.LastArgs[1]);
        Assert.Equal("darkfactory", fake.LastArgs[2]);
    }

    [Fact]
    public async Task CreateAsync_DefaultProfileIsMinikube()
    {
        var fake = new FakeProcessRunner
        {
            NextResult = new ProcessResult(0, "kubectl context will be \"minikube\"", string.Empty),
        };
        IClusterCreator creator = new MinikubeClusterCreator(fake);

        var result = await creator.CreateAsync();

        Assert.True(result.Success);
        Assert.Equal("minikube", result.Profile);
        Assert.Equal("minikube", result.KubeContext);
        Assert.Equal(3, fake.LastArgs!.Count);
        Assert.Equal("start", fake.LastArgs[0]);
        Assert.Equal("-p", fake.LastArgs[1]);
        Assert.Equal("minikube", fake.LastArgs[2]);
    }

    [Fact]
    public async Task AC3_CreateAsync_NonZeroExit_ReturnsFailureWithStderr()
    {
        var fake = new FakeProcessRunner { NextResult = new ProcessResult(1, string.Empty, "Docker is not running") };
        IClusterCreator creator = new MinikubeClusterCreator(fake);

        var result = await creator.CreateAsync("darkfactory");

        Assert.False(result.Success);
        Assert.Equal(ClusterState.Failed, result.State);
        Assert.Contains("Docker is not running", result.Errors);
    }

    [Fact]
    public async Task AC3_CreateAsync_RunnerException_ReturnsFailure()
    {
        var fake = new FakeProcessRunner { ThrowOnRun = new System.InvalidOperationException("boom") };
        IClusterCreator creator = new MinikubeClusterCreator(fake);

        var result = await creator.CreateAsync("darkfactory");

        Assert.False(result.Success);
        Assert.Contains("boom", result.Errors[0]);
    }

    [Fact]
    public async Task AC4_TeardownAsync_LeaveUp_RunsMinikubeStop()
    {
        var fake = new FakeProcessRunner { NextResult = new ProcessResult(0, string.Empty, string.Empty) };
        IClusterCreator creator = new MinikubeClusterCreator(fake);

        var result = await creator.TeardownAsync("darkfactory", ClusterTeardownPreference.LeaveUp);

        Assert.True(result.Success);
        Assert.Equal(3, fake.LastArgs!.Count);
        Assert.Equal("stop", fake.LastArgs[0]);
        Assert.Equal("-p", fake.LastArgs[1]);
        Assert.Equal("darkfactory", fake.LastArgs[2]);
    }

    [Fact]
    public async Task AC4_TeardownAsync_TearDown_RunsMinikubeDelete()
    {
        var fake = new FakeProcessRunner { NextResult = new ProcessResult(0, string.Empty, string.Empty) };
        IClusterCreator creator = new MinikubeClusterCreator(fake);

        var result = await creator.TeardownAsync("darkfactory", ClusterTeardownPreference.TearDown);

        Assert.True(result.Success);
        Assert.Equal(3, fake.LastArgs!.Count);
        Assert.Equal("delete", fake.LastArgs[0]);
        Assert.Equal("-p", fake.LastArgs[1]);
        Assert.Equal("darkfactory", fake.LastArgs[2]);
    }

    [Fact]
    public void AC2_CliRunner_BuildCommand_AppendsKubeContext()
    {
        var cmd = CliRunner.BuildCommand(SampleRequest(), "darkfactory");
        Assert.Contains("--kube-context darkfactory", cmd);
    }

    [Fact]
    public void AC2_CliRunner_BuildCommand_OneArg_Unchanged_RegressesDocDriftGuard()
    {
        // The #16 doc-drift guard asserts README documents this exact form; it must stay stable.
        var cmd = CliRunner.BuildCommand(SampleRequest());
        Assert.Equal("df:sandbox-up --repo owner/repo --issue 1", cmd);
    }
}

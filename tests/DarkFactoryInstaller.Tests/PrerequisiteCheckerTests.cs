using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class PrerequisiteCheckerTests
{
    private sealed class FakeCli : ISandboxCli
    {
        private readonly CheckRunResult _result;
        public FakeCli(CheckRunResult result) => _result = result;
        public Task<CheckRunResult> RunCheckAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class ThrowingCli : ISandboxCli
    {
        public Task<CheckRunResult> RunCheckAsync(CancellationToken cancellationToken = default)
            => throw new System.InvalidOperationException("df:sandbox-up not on PATH");
    }

    [Fact]
    public async Task Ready_WhenExitCodeZero_AllToolsPresent()
    {
        var cli = new FakeCli(new CheckRunResult(0,
            new[] { "{\"step\":\"done\",\"phase\":\"success\",\"exitCode\":0}" },
            System.Array.Empty<string>()));
        var checker = new PrerequisiteChecker(cli);

        var result = await checker.CheckAsync();

        Assert.True(result.Ready);
        Assert.All(result.Statuses, s => Assert.True(s.Present));
        Assert.Empty(result.RawProblems);
    }

    [Fact]
    public async Task NotReady_WhenExitCodeTen_SurfacesMissingTools()
    {
        var stdout = new[] { "{\"step\":\"done\",\"phase\":\"failure\",\"exitCode\":10}" };
        var stderr = new[]
        {
            "ERROR: docker is not installed",
            "ERROR: Install Docker.",
            "ERROR: helm version 3.10.0 does not satisfy >=3.14.0",
            "ERROR: Upgrade Helm to >=3.14.0.",
        };
        var cli = new FakeCli(new CheckRunResult(10, stdout, stderr));
        var checker = new PrerequisiteChecker(cli);

        var result = await checker.CheckAsync();

        Assert.False(result.Ready);
        var docker = Assert.Single(result.Statuses.Where(s => s.Name == "Docker"));
        Assert.False(docker.Present);
        Assert.Contains("docker is not installed", docker.Message!);
        var helm = Assert.Single(result.Statuses.Where(s => s.Name == "Helm"));
        Assert.False(helm.Present);
        Assert.All(result.Statuses.Where(s => s.Name == "kubectl" || s.Name == "Python" || s.Name == "minikube"),
            s => Assert.True(s.Present));
        Assert.Equal(4, result.RawProblems.Count);
    }

    [Fact]
    public async Task FailureToRun_IsReportedAsNotReady()
    {
        var cli = new ThrowingCli();
        var checker = new PrerequisiteChecker(cli);

        var result = await checker.CheckAsync();

        Assert.False(result.Ready);
        Assert.NotEmpty(result.RawProblems);
    }
}

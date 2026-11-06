using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class CliRunnerTests
{
    [Fact]
    public void BuildCommand_IncludesRepoAndIssueFlags()
    {
        SandboxRunRequest.TryCreate("ricardoblackskye/agent-eve", 42, out var req, out _);
        Assert.NotNull(req);
        var cmd = CliRunner.BuildCommand(req!);
        Assert.StartsWith("df:sandbox-up", cmd);
        Assert.Contains("--repo ricardoblackskye/agent-eve", cmd);
        Assert.Contains("--issue 42", cmd);
    }

    [Fact]
    public void BuildCommand_HandlesHyphenAndDotInRepo()
    {
        SandboxRunRequest.TryCreate("my-org/my.repo", 7, out var req, out _);
        Assert.NotNull(req);
        var cmd = CliRunner.BuildCommand(req!);
        Assert.Contains("--repo my-org/my.repo", cmd);
        Assert.Contains("--issue 7", cmd);
    }
}

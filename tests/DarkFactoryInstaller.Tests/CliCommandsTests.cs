using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class CliCommandsTests
{
    [Fact]
    public void UpIncludesWorkspaceRepoIssue()
    {
        var (exe, args) = CliCommands.Up("owner/repo", 42, @"C:\wf\ws");
        Assert.Equal("npm", exe);
        Assert.Contains("--workspace-dir", args);
        Assert.Contains(@"C:\wf\ws", args);
        Assert.Contains("--repo", args);
        Assert.Contains("owner/repo", args);
        Assert.Contains("--issue", args);
        Assert.Contains("42", args);
    }

    [Fact]
    public void DownDeleteClusterAddsFlag()
    {
        var (exe, args) = CliCommands.Down(deleteCluster: true);
        Assert.Equal("npm", exe);
        Assert.Contains("--delete-cluster", args);
        var (_, args2) = CliCommands.Down(deleteCluster: false);
        Assert.DoesNotContain("--delete-cluster", args2);
    }
}
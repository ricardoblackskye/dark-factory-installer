using DarkFactoryInstaller.Domain;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class SandboxRunRequestTests
{
    [Fact]
    public void ValidOwnerRepoAndPositiveIssue_CreatesRequest()
    {
        var ok = SandboxRunRequest.TryCreate("ricardoblackskye/agent-eve", 42, out var req, out var err);
        Assert.True(ok);
        Assert.NotNull(req);
        Assert.Equal("ricardoblackskye/agent-eve", req!.Repo);
        Assert.Equal(42, req.Issue);
        Assert.Null(err);
    }

    [Theory]
    [InlineData("bad_repo")]   // no slash
    [InlineData("owner/")]     // empty name
    [InlineData("/repo")]      // empty owner
    [InlineData("owner/ repo")] // space
    [InlineData("")]           // empty
    public void MalformedRepo_Rejected(string repo)
    {
        var ok = SandboxRunRequest.TryCreate(repo, 1, out _, out var err);
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(err));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveIssue_Rejected(int issue)
    {
        var ok = SandboxRunRequest.TryCreate("owner/repo", issue, out _, out var err);
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(err));
    }
}

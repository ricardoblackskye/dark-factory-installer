using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class ExitCodeMappingTests
{
    [Fact]
    public void ZeroIsNotAnError() => Assert.Null(ExitCodeMapping.Map(0));

    [Fact]
    public void SecretOnArgv_IsOperatorFixable()
    {
        var info = ExitCodeMapping.Map(2);
        Assert.NotNull(info);
        Assert.Equal(ErrorCategory.OperatorFixable, info!.Category);
        Assert.False(string.IsNullOrWhiteSpace(info.Message));
        Assert.Contains("secret", info.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrereqNotReady_IsOperatorFixable()
    {
        var info = ExitCodeMapping.Map(10);
        Assert.NotNull(info);
        Assert.Equal(ErrorCategory.OperatorFixable, info!.Category);
        Assert.Contains("Prerequisites", info.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownExit_IsInternal()
    {
        var info = ExitCodeMapping.Map(137);
        Assert.NotNull(info);
        Assert.Equal(ErrorCategory.Internal, info!.Category);
        Assert.Contains("unexpected", info.Message, StringComparison.OrdinalIgnoreCase);
    }
}

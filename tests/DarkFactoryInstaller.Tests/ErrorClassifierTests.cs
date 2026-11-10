using System.Collections.Generic;
using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class ErrorClassifierTests
{
    private static readonly ProvisionProblem PrereqMissing = new() { Code = ProvisionProblemCode.PrerequisitesMissing };
    private static readonly ProvisionProblem InternalProb = new() { Code = ProvisionProblemCode.Internal };

    [Fact]
    public void Problems_OperatorFixableCode_IsClassifiedOperatorFixable()
    {
        var report = ErrorClassifier.Classify(new ProcessResult(10, string.Empty, string.Empty), new[] { PrereqMissing });
        Assert.NotNull(report);
        Assert.Equal(ErrorCategory.OperatorFixable, report!.Category);
        Assert.DoesNotContain("exit=", report.Diagnostics ?? string.Empty);
    }

    [Fact]
    public void Problems_InternalCode_IsClassifiedInternal_WithDiagnostics()
    {
        var report = ErrorClassifier.Classify(new ProcessResult(1, string.Empty, string.Empty), new[] { InternalProb });
        Assert.NotNull(report);
        Assert.Equal(ErrorCategory.Internal, report!.Category);
        Assert.Contains("exit=", report.Diagnostics ?? string.Empty);
    }

    [Fact]
    public void NoProblems_Exit10_IsOperatorFixable()
    {
        var report = ErrorClassifier.Classify(new ProcessResult(10, string.Empty, string.Empty));
        Assert.NotNull(report);
        Assert.Equal(ErrorCategory.OperatorFixable, report!.Category);
    }

    [Fact]
    public void NoProblems_Exit0_IsSuccess_Null()
    {
        var report = ErrorClassifier.Classify(new ProcessResult(0, string.Empty, string.Empty));
        Assert.Null(report);
    }

    [Fact]
    public void NoProblems_UnknownExit_IsInternal_WithDiagnostics()
    {
        var report = ErrorClassifier.Classify(new ProcessResult(139, string.Empty, string.Empty), null, "deploy", "start");
        Assert.NotNull(report);
        Assert.Equal(ErrorCategory.Internal, report!.Category);
        Assert.Contains("exit=139", report.Diagnostics ?? string.Empty);
        Assert.Contains("step=deploy", report.Diagnostics ?? string.Empty);
    }
}

namespace DarkFactoryInstaller.Domain;

/// <summary>Pipeline stages of a sandbox run, for observability and ordering (issue #7, AC6).</summary>
public enum OrchestrationStage
{
    NotStarted,
    VersionGate,
    Prerequisites,
    Cluster,
    Check,
    Provision,
    Cancelled,
    Completed,
}

/// <summary>One stage of the orchestration pipeline that was reached, with its outcome.</summary>
public sealed record OrchestrationStep(OrchestrationStage Stage, bool Succeeded, IReadOnlyList<string> Errors);

/// <summary>Result of running the full sandbox orchestration (issue #7).</summary>
public sealed record OrchestrationResult(
    bool Success,
    string? KubeContext,
    int? ExitCode,
    OrchestrationStage FailedStage,
    IReadOnlyList<string> Errors,
    IReadOnlyList<OrchestrationStep> Steps)
{
    public static OrchestrationResult Failure(
        OrchestrationStage failedStage, IReadOnlyList<string> errors, IReadOnlyList<OrchestrationStep> steps, string? kubeContext = null)
        => new(false, kubeContext, null, failedStage, errors, steps);

    public static OrchestrationResult Cancelled(IReadOnlyList<OrchestrationStep> steps, string? kubeContext = null)
        => new(false, kubeContext, null, OrchestrationStage.Cancelled, new[] { "Orchestration was cancelled." }, steps);

    public static OrchestrationResult Succeeded(string? kubeContext, int exitCode, IReadOnlyList<OrchestrationStep> steps)
        => new(true, kubeContext, exitCode, OrchestrationStage.Completed, System.Array.Empty<string>(), steps);
}

/// <summary>Result of the CLI version gate (issue #7, AC4).</summary>
public sealed record VersionGateResult(bool Supported, string? DetectedVersion, IReadOnlyList<string> Errors)
{
    public static VersionGateResult Ok(string? detected) => new(true, detected, System.Array.Empty<string>());
    public static VersionGateResult Unsupported(string? detected, IReadOnlyList<string> errors) => new(false, detected, errors);
}

using System.Collections.Generic;

namespace DarkFactoryInstaller.Domain;

/// <summary>Outcome of the last provisioning attempt, for the installer's persisted progress (issue #9).</summary>
public enum ProvisionOutcome
{
    InProgress,
    Succeeded,
    Failed,
}

/// <summary>
/// Persisted, per-target provisioning progress. Contains only non-secret data: the target id,
/// which steps completed, the last outcome, and a timestamp (issue #9, AC4).
/// </summary>
public sealed record ProvisionState
{
    public string TargetId { get; init; } = string.Empty;
    public HashSet<ProvisionStep> CompletedSteps { get; init; } = new();
    public ProvisionOutcome Outcome { get; init; } = ProvisionOutcome.InProgress;
    public string Timestamp { get; init; } = string.Empty;
}
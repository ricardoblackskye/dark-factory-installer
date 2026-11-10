namespace DarkFactoryInstaller.Domain;

/// <summary>Provisioning stages emitted by agent-eve's headless CLI (sandbox-events.ts, #582/#585).</summary>
public enum ProvisionStep
{
    Check,
    Vendor,
    Cluster,
    Deploy,
    Forward,
    Proof,
    Teardown,
    Done,
}

/// <summary>Per-stage outcome (sandbox-events.ts ProvisionPhase).</summary>
public enum ProvisionPhase
{
    Start,
    Success,
    Failure,
    Skip,
}

/// <summary>Stable failure classes (sandbox-provisioning.ts ProvisionProblemCode, #585 T1).</summary>
public enum ProvisionProblemCode
{
    PrerequisitesMissing,
    PrerequisitesMismatch,
    ArtifactUnresolvable,
    ClusterUnreachable,
    ProofFailed,
    TeardownFailed,
    Internal,
}

/// <summary>One classified problem from the CLI: code for machines, subject/fix for operators.</summary>
public sealed record ProvisionProblem
{
    public ProvisionProblemCode Code { get; init; }
    public string? Subject { get; init; }
    public string? Fix { get; init; }
}

/// <summary>A single NDJSON event line from the CLI (sandbox-events.ts ProvisionEvent).</summary>
public sealed record ProvisionEvent
{
    public string Ts { get; init; } = string.Empty;
    public ProvisionStep Step { get; init; }
    public ProvisionPhase Phase { get; init; }
    public string Message { get; init; } = string.Empty;
    public int? ExitCode { get; init; }
    public IReadOnlyList<ProvisionProblem> Problems { get; init; } = System.Array.Empty<ProvisionProblem>();
}
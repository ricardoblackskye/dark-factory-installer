namespace DarkFactoryInstaller.Domain;

/// <summary>Lifecycle state of a locally-created cluster (issue #6).</summary>
public enum ClusterState
{
    Unknown,
    Created,
    Failed,
    Stopped,
    Deleted,
}

/// <summary>Operator's teardown choice when a run finishes (issue #6, AC4).</summary>
public enum ClusterTeardownPreference
{
    /// <summary>Leave the cluster running but release the namespace/port (minikube stop).</summary>
    LeaveUp,
    /// <summary>Tear the cluster down completely (minikube delete).</summary>
    TearDown,
}

/// <summary>Result of creating a local cluster (issue #6, AC1/AC3).</summary>
public sealed record ClusterCreationResult(
    bool Success,
    string? Profile,
    string? KubeContext,
    ClusterState State,
    IReadOnlyList<string> Errors)
{
    public static ClusterCreationResult Failure(string? profile, IReadOnlyList<string> errors)
        => new(false, profile, null, ClusterState.Failed, errors);
}

/// <summary>Result of tearing a cluster down (issue #6, AC4).</summary>
public sealed record ClusterTeardownResult(bool Success, IReadOnlyList<string> Errors);

/// <summary>Observable snapshot of a created cluster, consumed by the UI (#8) and retry (#9).</summary>
public sealed record ClusterInfo(string Profile, string KubeContext, ClusterState State);

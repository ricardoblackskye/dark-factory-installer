using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Abstraction over creating/tearing down the local cluster the installer targets, so the logic is
/// unit-testable without a real minikube/Docker process (issue #6). The captured kube context is later
/// passed to <c>df:sandbox-up --kube-context</c>.
/// </summary>
public interface IClusterCreator
{
    /// <summary>Runs <c>minikube start</c>, capturing the resulting kube context. Default profile is "minikube".</summary>
    Task<ClusterCreationResult> CreateAsync(string? profile = null, CancellationToken cancellationToken = default);

    /// <summary>Tears the cluster down per the operator's preference (AC4).</summary>
    Task<ClusterTeardownResult> TeardownAsync(string profile, ClusterTeardownPreference preference, CancellationToken cancellationToken = default);
}

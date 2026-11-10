using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Maps a stable <see cref="ProvisionProblemCode"/> to a clear, user-facing, actionable message
/// (issue #8, AC4). The mapping is by code, never by string-matching CLI prose. The seven documented
/// codes are wired here; the broader catalog is owned by issue #10.
/// </summary>
public static class ProblemMessageMapper
{
    public static string ToMessage(ProvisionProblemCode code) => code switch
    {
        ProvisionProblemCode.PrerequisitesMissing =>
            "A prerequisite is missing or unusable (Docker, kubectl, helm, python, or minikube). Run the prerequisite check and follow its fix.",
        ProvisionProblemCode.PrerequisitesMismatch =>
            "A prerequisite is present but its version does not match the manifest. Update it to the required version.",
        ProvisionProblemCode.ArtifactUnresolvable =>
            "An artifact could not be resolved (missing or invalid lock, empty cache, or image not in the local store). Run the vendor step.",
        ProvisionProblemCode.ClusterUnreachable =>
            "The cluster is unreachable (minikube failed to start, or the kube context did not answer). Check the cluster.",
        ProvisionProblemCode.ProofFailed =>
            "The vault proof failed: the endpoint was unreachable, or credential substitution was not demonstrated.",
        ProvisionProblemCode.TeardownFailed =>
            "Teardown failed (port-forward or release cleanup did not complete).",
        _ =>
            "An internal error occurred during provisioning.",
    };
}
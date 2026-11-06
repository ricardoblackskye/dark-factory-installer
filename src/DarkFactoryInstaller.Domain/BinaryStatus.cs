namespace DarkFactoryInstaller.Domain;

/// <summary>Outcome of verifying / acquiring one binary (issue #5).</summary>
public enum BinaryState
{
    /// <summary>Operator-installed binary present and version satisfies the constraint.</summary>
    PresentVerified,
    /// <summary>Binary not found / probe produced no version.</summary>
    Missing,
    /// <summary>Binary found but version does not satisfy the constraint.</summary>
    VersionMismatch,
    /// <summary>Program downloaded + verified (digest) a <c>acquiredBy: "program"</c> binary.</summary>
    Acquired,
    /// <summary>Digest of a downloaded binary did not match <c>expectedDigest</c>.</summary>
    DigestMismatch,
}

/// <summary>
/// Resolved state of one binary for the UI (#8) and error handling (#10): whether it is satisfied
/// and whether it blocks provisioning. Optional binaries (e.g. minikube) only block when the
/// selected cluster mode matches <see cref="RequiredFor"/> (wired in #6/#7).
/// </summary>
public sealed record BinaryStatus(
    string Name,
    bool Required,
    BinaryState State,
    string? DetectedVersion,
    string? ExpectedConstraint,
    string? Message,
    string? RequiredFor,
    bool BlocksProvision);

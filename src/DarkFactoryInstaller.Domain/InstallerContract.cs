namespace DarkFactoryInstaller.Domain;

/// <summary>
/// Contract constants the installer enforces against agent-eve (portfolio epic #584, issue #24:
/// version coupling). Bump <see cref="MinimumCliVersion"/> when agent-eve ships a released semver.
/// Today agent-eve is still <c>0.0.0</c> (dev), so this is a structural floor that refuses
/// checkouts too old to report a version (pre-#582 T1) without rejecting current dev checkouts.
/// </summary>
public static class InstallerContract
{
    /// <summary>
    /// Minimum agent-eve CLI version the installer will provision against, as a SemVer constraint
    /// string understood by <c>VersionConstraint</c>. Declared here as the single source of truth.
    /// </summary>
    public const string MinimumCliVersion = ">=0.0.0";
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Verifies one operator-provided binary: runs its manifest <c>probe</c>, extracts the first SemVer
/// token from the output, and checks it against <c>versionConstraint</c>. Never downloads (operator
/// binaries are acquired by the operator, per the manifest's <c>acquiredBy: "operator"</c>). Issue #5.
/// </summary>
public sealed class BinaryVerifier
{
    private readonly IProcessRunner _processRunner;

    public BinaryVerifier(IProcessRunner processRunner) => _processRunner = processRunner;

    public async Task<BinaryStatus> VerifyAsync(BinaryEntry entry, bool blocksProvision, CancellationToken cancellationToken = default)
    {
        if (entry.Probe is null)
        {
            return new BinaryStatus(entry.Name, entry.Required, BinaryState.Missing, null, entry.VersionConstraint,
                entry.Fix ?? $"{entry.Name} is required but declares no probe.", entry.RequiredFor, blocksProvision);
        }

        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(entry.Probe.Command, entry.Probe.Args, cancellationToken).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            return new BinaryStatus(entry.Name, entry.Required, BinaryState.Missing, null, entry.VersionConstraint,
                $"{entry.Name} could not be run: {ex.Message}. {entry.Fix ?? ""}", entry.RequiredFor, blocksProvision);
        }

        var detected = SemVer.ExtractFirst(result.Stdout + "\n" + result.Stderr);
        if (detected is null)
        {
            return new BinaryStatus(entry.Name, entry.Required, BinaryState.Missing, null, entry.VersionConstraint,
                entry.Fix ?? $"{entry.Name} not found or reported no version.", entry.RequiredFor, blocksProvision);
        }

        var constraint = VersionConstraint.Parse(entry.VersionConstraint);
        if (!constraint.IsSatisfied(detected.Value))
        {
            return new BinaryStatus(entry.Name, entry.Required, BinaryState.VersionMismatch, detected.Value.ToString(),
                entry.VersionConstraint, entry.Fix ?? $"{entry.Name} version {detected} does not satisfy {entry.VersionConstraint}.",
                entry.RequiredFor, blocksProvision);
        }

        return new BinaryStatus(entry.Name, entry.Required, BinaryState.PresentVerified, detected.Value.ToString(),
            entry.VersionConstraint, null, entry.RequiredFor, blocksProvision);
    }
}

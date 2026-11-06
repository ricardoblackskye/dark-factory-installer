using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Verifies every manifest binary via <see cref="BinaryVerifier"/>. An entry blocks provisioning when it is
/// required (or is required-for the selected cluster mode; #6/#7 wire the mode). Issue #5/#7.
/// </summary>
public sealed class ManifestPrereqValidator : IPrerequisiteValidator
{
    private readonly IBinaryManifestSource _manifestSource;
    private readonly BinaryVerifier _verifier;

    public ManifestPrereqValidator(IBinaryManifestSource manifestSource, BinaryVerifier verifier)
    {
        _manifestSource = manifestSource;
        _verifier = verifier;
    }

    public async Task<PrereqCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await _manifestSource.LoadAsync(cancellationToken).ConfigureAwait(false);
        var errors = new System.Collections.Generic.List<string>();
        foreach (var entry in manifest.Binaries)
        {
            var blocks = entry.Required || entry.RequiredFor is not null;
            var status = await _verifier.VerifyAsync(entry, blocks, cancellationToken).ConfigureAwait(false);
            if (status.State != BinaryState.PresentVerified && blocks)
            {
                errors.Add(status.Message ?? $"{entry.Name} is not satisfied.");
            }
        }

        return new PrereqCheckResult(errors.Count == 0, errors);
    }
}

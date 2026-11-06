using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Orchestrates binary acquisition/verification for issue #5: loads the agent-eve manifest, then for
/// each entry verifies operator-installed binaries (via <see cref="BinaryVerifier"/>) or acquires +
/// digest-verifies program binaries (via <see cref="BinaryDownloader"/>). Produces a
/// <see cref="BinaryStatus"/> per binary, exposing the resolved state for the UI (#8) and error
/// handling (#10), and runs before provision orchestration (#7) so a missing binary blocks up front.
/// </summary>
public sealed class BinaryAcquisitionService
{
    private readonly IBinaryManifestSource _manifestSource;
    private readonly IProcessRunner _processRunner;
    private readonly IFileDownloader _downloader;

    public BinaryAcquisitionService(IBinaryManifestSource manifestSource, IProcessRunner processRunner, IFileDownloader downloader)
    {
        _manifestSource = manifestSource;
        _processRunner = processRunner;
        _downloader = downloader;
    }

    public async Task<IReadOnlyList<BinaryStatus>> AcquireAsync(string? selectedClusterMode = null, CancellationToken cancellationToken = default)
    {
        var manifest = await _manifestSource.LoadAsync(cancellationToken).ConfigureAwait(false);
        var verifier = new BinaryVerifier(_processRunner);
        var downloader = new BinaryDownloader(_downloader);
        var statuses = new List<BinaryStatus>();

        foreach (var entry in manifest.Binaries)
        {
            var blocks = BlocksProvision(entry, selectedClusterMode);
            BinaryStatus status = (entry.AcquiredBy == "program" && !string.IsNullOrEmpty(entry.Source))
                ? await downloader.AcquireAsync(entry, blocks, cancellationToken).ConfigureAwait(false)
                : await verifier.VerifyAsync(entry, blocks, cancellationToken).ConfigureAwait(false);
            statuses.Add(status);
        }

        return statuses;
    }

    private static bool BlocksProvision(BinaryEntry entry, string? selectedClusterMode)
    {
        if (entry.Required) return true;
        return !string.IsNullOrEmpty(entry.RequiredFor)
            && string.Equals(entry.RequiredFor, selectedClusterMode, System.StringComparison.OrdinalIgnoreCase);
    }
}

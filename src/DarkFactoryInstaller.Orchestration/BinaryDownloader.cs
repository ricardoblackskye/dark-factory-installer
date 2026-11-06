using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Acquires a <c>acquiredBy: "program"</c> binary: downloads it from the manifest's <c>source</c>
/// and verifies its sha256 against <c>expectedDigest</c>. Hard-fails on mismatch (AC3). Currently
/// inert — the #603 manifest marks every binary <c>acquiredBy: "operator"</c>; this path activates
/// when a future manifest entry names a program-acquirable binary. Issue #5.
/// </summary>
public sealed class BinaryDownloader
{
    private readonly IFileDownloader _downloader;

    public BinaryDownloader(IFileDownloader downloader) => _downloader = downloader;

    public async Task<BinaryStatus> AcquireAsync(BinaryEntry entry, bool blocksProvision, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await _downloader.DownloadAsync(entry.Source!, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(entry.ExpectedDigest))
            {
                var actual = "sha256:" + ComputeSha256(bytes);
                if (!string.Equals(actual, entry.ExpectedDigest, StringComparison.OrdinalIgnoreCase))
                {
                    return new BinaryStatus(entry.Name, entry.Required, BinaryState.DigestMismatch, null, entry.VersionConstraint,
                        $"Digest mismatch for {entry.Name}: expected {entry.ExpectedDigest}, got {actual}. {entry.Fix ?? ""}",
                        entry.RequiredFor, blocksProvision);
                }
            }

            return new BinaryStatus(entry.Name, entry.Required, BinaryState.Acquired, null, entry.VersionConstraint,
                null, entry.RequiredFor, blocksProvision);
        }
        catch (Exception ex)
        {
            return new BinaryStatus(entry.Name, entry.Required, BinaryState.Missing, null, entry.VersionConstraint,
                $"Failed to acquire {entry.Name}: {ex.Message}. {entry.Fix ?? ""}", entry.RequiredFor, blocksProvision);
        }
    }

    public static string ComputeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}

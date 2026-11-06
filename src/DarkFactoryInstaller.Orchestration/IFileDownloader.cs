using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>Downloads a binary's bytes from a URL (injectable for tests). Issue #5.</summary>
public interface IFileDownloader
{
    Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>Real HTTP downloader used by the app.</summary>
public sealed class SystemFileDownloader : IFileDownloader
{
    public async Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        return await client.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
    }
}

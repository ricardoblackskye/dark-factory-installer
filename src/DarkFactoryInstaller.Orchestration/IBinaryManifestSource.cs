using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>Supplies the agent-eve manifest the program verifies binaries against (issue #5).</summary>
public interface IBinaryManifestSource
{
    Task<BinaryManifest> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads a committed snapshot of <c>artifacts.lock.json</c> from disk. The manifest is a lockfile
/// copied from agent-eve (#603); the program carries the copy, not the binaries (AC5).
/// </summary>
public sealed class FileBinaryManifestSource : IBinaryManifestSource
{
    private readonly string _path;

    public FileBinaryManifestSource(string path) => _path = path;

    public async Task<BinaryManifest> LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<BinaryManifest>(json, options) ?? new BinaryManifest();
    }
}

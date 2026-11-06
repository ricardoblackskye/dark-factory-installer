using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class BinaryAcquisitionTests
{
    // ---- fakes ----------------------------------------------------------------

    private sealed class MapManifestSource : IBinaryManifestSource
    {
        private readonly BinaryManifest _manifest;
        public MapManifestSource(BinaryManifest manifest) => _manifest = manifest;
        public Task<BinaryManifest> LoadAsync(CancellationToken ct = default) => Task.FromResult(_manifest);
    }

    private sealed class MapProcessRunner : IProcessRunner
    {
        private readonly Dictionary<string, ProcessResult> _map;
        public MapProcessRunner(Dictionary<string, ProcessResult> map) => _map = map;
        public Task<ProcessResult> RunAsync(string command, IReadOnlyList<string> args, CancellationToken ct = default)
            => Task.FromResult(_map.TryGetValue(command, out var r) ? r : new ProcessResult(0, "", ""));
    }

    private sealed class FixedDownloader : IFileDownloader
    {
        private readonly byte[] _bytes;
        public FixedDownloader(byte[] bytes) => _bytes = bytes;
        public Task<byte[]> DownloadAsync(string url, CancellationToken ct = default) => Task.FromResult(_bytes);
    }

    // ---- primitives -----------------------------------------------------------

    [Fact]
    public void VersionConstraint_SatisfiedForGreaterEqual()
    {
        Assert.True(VersionConstraint.Parse(">=20.10.0").IsSatisfied(SemVer.Parse("20.10.0")));
        Assert.True(VersionConstraint.Parse(">=20.10.0").IsSatisfied(SemVer.Parse("21.0.0")));
        Assert.False(VersionConstraint.Parse(">=20.10.0").IsSatisfied(SemVer.Parse("20.9.0")));
    }

    [Fact]
    public void VersionConstraint_SupportsOtherOperators()
    {
        Assert.True(VersionConstraint.Parse(">1.0.0").IsSatisfied(SemVer.Parse("1.0.1")));
        Assert.False(VersionConstraint.Parse(">1.0.0").IsSatisfied(SemVer.Parse("1.0.0")));
        Assert.True(VersionConstraint.Parse("==1.2.3").IsSatisfied(SemVer.Parse("1.2.3")));
        Assert.True(VersionConstraint.Parse("<=2.0.0").IsSatisfied(SemVer.Parse("1.9.9")));
    }

    [Fact]
    public void SemVer_ExtractsFirstTokenFromToolOutput()
    {
        Assert.Equal("1.29.0", SemVer.ExtractFirst("Client Version: v1.29.0")!.Value.ToString());
        Assert.Equal("3.14.0", SemVer.ExtractFirst("v3.14.0")!.Value.ToString());
        Assert.Equal("20.10.24", SemVer.ExtractFirst("20.10.24")!.Value.ToString());
        Assert.Null(SemVer.ExtractFirst("no version here"));
    }

    // ---- verifier -------------------------------------------------------------

    [Fact]
    public async Task Verifier_PresentWhenVersionSatisfies()
    {
        var entry = new BinaryEntry
        {
            Name = "docker", VersionConstraint = ">=20.10.0", Required = true, AcquiredBy = "operator",
            Probe = new ProbeSpec { Command = "docker", Args = new[] { "version" } }, Fix = "Install Docker.",
        };
        var runner = new MapProcessRunner(new() { ["docker"] = new ProcessResult(0, "20.10.24", "") });
        var status = await new BinaryVerifier(runner).VerifyAsync(entry, true);

        Assert.Equal(BinaryState.PresentVerified, status.State);
        Assert.Equal("20.10.24", status.DetectedVersion);
    }

    [Fact]
    public async Task Verifier_MismatchWhenBelowConstraint()
    {
        var entry = new BinaryEntry
        {
            Name = "docker", VersionConstraint = ">=20.10.0", Required = true, AcquiredBy = "operator",
            Probe = new ProbeSpec { Command = "docker", Args = new[] { "version" } }, Fix = "Upgrade Docker.",
        };
        var runner = new MapProcessRunner(new() { ["docker"] = new ProcessResult(0, "20.9.0", "") });
        var status = await new BinaryVerifier(runner).VerifyAsync(entry, true);

        Assert.Equal(BinaryState.VersionMismatch, status.State);
        Assert.True(status.BlocksProvision);
    }

    [Fact]
    public async Task Verifier_MissingWhenProbeProducesNoVersion()
    {
        var entry = new BinaryEntry
        {
            Name = "kubectl", VersionConstraint = ">=1.28.0", Required = true, AcquiredBy = "operator",
            Probe = new ProbeSpec { Command = "kubectl", Args = new[] { "version" } }, Fix = "Install kubectl.",
        };
        var runner = new MapProcessRunner(new() { ["kubectl"] = new ProcessResult(0, "", "") });
        var status = await new BinaryVerifier(runner).VerifyAsync(entry, true);

        Assert.Equal(BinaryState.Missing, status.State);
        Assert.Contains("kubectl", status.Message!);
    }

    // ---- downloader -----------------------------------------------------------

    [Fact]
    public async Task Downloader_AcquiresWhenDigestMatches()
    {
        var bytes = Encoding.UTF8.GetBytes("fake-binary-bytes");
        var digest = "sha256:" + BinaryDownloader.ComputeSha256(bytes);
        var entry = new BinaryEntry
        {
            Name = "tool", VersionConstraint = ">=1.0.0", Required = true, AcquiredBy = "program",
            Source = "https://example.com/tool", ExpectedDigest = digest, Fix = "Retry download.",
        };
        var status = await new BinaryDownloader(new FixedDownloader(bytes)).AcquireAsync(entry, true);

        Assert.Equal(BinaryState.Acquired, status.State);
    }

    [Fact]
    public async Task Downloader_DigestMismatchIsHardFailure()
    {
        var bytes = Encoding.UTF8.GetBytes("fake-binary-bytes");
        var entry = new BinaryEntry
        {
            Name = "tool", VersionConstraint = ">=1.0.0", Required = true, AcquiredBy = "program",
            Source = "https://example.com/tool", ExpectedDigest = "sha256:deadbeef", Fix = "Retry download.",
        };
        var status = await new BinaryDownloader(new FixedDownloader(bytes)).AcquireAsync(entry, true);

        Assert.Equal(BinaryState.DigestMismatch, status.State);
        Assert.True(status.BlocksProvision);
    }

    // ---- service --------------------------------------------------------------

    private static BinaryManifest SampleManifest()
    {
        return new BinaryManifest
        {
            Binaries = new List<BinaryEntry>
            {
                new() { Name = "docker", VersionConstraint = ">=20.10.0", Required = true, AcquiredBy = "operator",
                        Probe = new ProbeSpec { Command = "docker", Args = new[] { "version" } }, Fix = "Install Docker." },
                new() { Name = "kubectl", VersionConstraint = ">=1.28.0", Required = true, AcquiredBy = "operator",
                        Probe = new ProbeSpec { Command = "kubectl", Args = new[] { "version" } }, Fix = "Install kubectl." },
                new() { Name = "minikube", VersionConstraint = ">=1.30.0", Required = false, RequiredFor = "minikube",
                        AcquiredBy = "operator", Probe = new ProbeSpec { Command = "minikube", Args = new[] { "version" } }, Fix = "Install minikube." },
            },
        };
    }

    [Fact]
    public async Task Service_ReportsOperatorBinariesAndGatesRequired()
    {
        var manifest = SampleManifest();
        var runner = new MapProcessRunner(new()
        {
            ["docker"] = new ProcessResult(0, "20.10.24", ""),
            ["kubectl"] = new ProcessResult(0, "", ""),
            ["minikube"] = new ProcessResult(0, "", ""),
        });
        var svc = new BinaryAcquisitionService(new MapManifestSource(manifest), runner, new FixedDownloader(System.Array.Empty<byte>()));

        var statuses = await svc.AcquireAsync();

        var docker = Assert.Single(statuses.Where(s => s.Name == "docker"));
        Assert.Equal(BinaryState.PresentVerified, docker.State);
        var kubectl = Assert.Single(statuses.Where(s => s.Name == "kubectl"));
        Assert.Equal(BinaryState.Missing, kubectl.State);
        Assert.True(kubectl.BlocksProvision); // required + not satisfied
        var minikube = Assert.Single(statuses.Where(s => s.Name == "minikube"));
        Assert.Equal(BinaryState.Missing, minikube.State);
        Assert.False(minikube.BlocksProvision); // optional, no cluster mode selected
    }

    [Fact]
    public async Task Service_OptionalBlocksWhenClusterModeSelected()
    {
        var manifest = SampleManifest();
        var runner = new MapProcessRunner(new()
        {
            ["docker"] = new ProcessResult(0, "20.10.24", ""),
            ["kubectl"] = new ProcessResult(0, "1.29.0", ""),
            ["minikube"] = new ProcessResult(0, "", ""),
        });
        var svc = new BinaryAcquisitionService(new MapManifestSource(manifest), runner, new FixedDownloader(System.Array.Empty<byte>()));

        var statuses = await svc.AcquireAsync(selectedClusterMode: "minikube");

        var minikube = Assert.Single(statuses.Where(s => s.Name == "minikube"));
        Assert.True(minikube.BlocksProvision); // optional but selected mode needs it
    }
}

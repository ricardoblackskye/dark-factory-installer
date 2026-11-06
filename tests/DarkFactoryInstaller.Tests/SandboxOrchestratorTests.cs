using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

internal sealed class RecordingSink : IOutputSink
{
    public List<string> StdoutLines { get; } = new();
    public List<string> StderrLines { get; } = new();
    public void OnStdout(string line) => StdoutLines.Add(line);
    public void OnStderr(string line) => StderrLines.Add(line);
}

internal sealed class FakeVersionGate : IVersionGate
{
    public bool Supported { get; set; } = true;
    public IReadOnlyList<string> Errors { get; set; } = System.Array.Empty<string>();
    public Task<VersionGateResult> CheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Supported ? VersionGateResult.Ok("1.0.0") : VersionGateResult.Unsupported("0.0.1", Errors));
}

internal sealed class FakePrereqValidator : IPrerequisiteValidator
{
    public bool Satisfied { get; set; } = true;
    public IReadOnlyList<string> Errors { get; set; } = System.Array.Empty<string>();
    public Task<PrereqCheckResult> CheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PrereqCheckResult(Satisfied, Errors));
}

internal sealed class FakeClusterCreator : IClusterCreator
{
    public bool Success { get; set; } = true;
    public string? KubeContext { get; set; } = "minikube";
    public bool CreateCalled { get; set; }
    public Task<ClusterCreationResult> CreateAsync(string? profile = null, CancellationToken cancellationToken = default)
    {
        CreateCalled = true;
        return Task.FromResult(Success
            ? new ClusterCreationResult(true, profile ?? "minikube", KubeContext, ClusterState.Created, System.Array.Empty<string>())
            : ClusterCreationResult.Failure(profile ?? "minikube", new[] { "cluster failed" }));
    }
    public Task<ClusterTeardownResult> TeardownAsync(string profile, ClusterTeardownPreference preference, CancellationToken cancellationToken = default)
        => Task.FromResult(new ClusterTeardownResult(true, System.Array.Empty<string>()));
}

internal sealed class FakeSandboxCli : ISandboxCli
{
    public int ExitCodeToReturn { get; set; } = 0;
    public IReadOnlyList<string> Stdout { get; set; } = System.Array.Empty<string>();
    public IReadOnlyList<string> Stderr { get; set; } = System.Array.Empty<string>();
    public bool Called { get; set; }
    public Task<CheckRunResult> RunCheckAsync(CancellationToken cancellationToken = default)
    {
        Called = true;
        return Task.FromResult(new CheckRunResult(ExitCodeToReturn, Stdout, Stderr));
    }
}

internal sealed class FakeArgvSecretGuard : IArgvSecretGuard
{
    public string? RefusalToReturn { get; set; }
    public string? LastCommandLine { get; set; }
    public string? RefuseIfSecretPresent(string commandLine)
    {
        LastCommandLine = commandLine;
        return RefusalToReturn;
    }
}

internal sealed class FakeProvisionRunner : IProvisionRunner
{
    public bool Started { get; set; }
    public SandboxRunRequest? LastRequest { get; set; }
    public string? LastKubeContext { get; set; }
    public IOutputSink? LastSink { get; set; }
    public bool CancelCalled { get; set; }
    public int ExitToReturn { get; set; } = 0;
    public IReadOnlyList<string> StdoutToEmit { get; set; } = System.Array.Empty<string>();
    public IReadOnlyList<string> StderrToEmit { get; set; } = System.Array.Empty<string>();

    public Task<IProvisionSession> StartAsync(SandboxRunRequest request, string? kubeContext, IOutputSink sink, CancellationToken cancellationToken)
    {
        Started = true;
        LastRequest = request;
        LastKubeContext = kubeContext;
        LastSink = sink;
        foreach (var line in StdoutToEmit)
        {
            sink.OnStdout(line);
        }
        foreach (var line in StderrToEmit)
        {
            sink.OnStderr(line);
        }
        return Task.FromResult<IProvisionSession>(new FakeSession(this, ExitToReturn));
    }

    private sealed class FakeSession : IProvisionSession
    {
        private readonly FakeProvisionRunner _owner;
        private readonly int _exit;
        private readonly TaskCompletionSource<int> _tcs = new();
        public FakeSession(FakeProvisionRunner owner, int exit) { _owner = owner; _exit = exit; }
        public Task<int> WaitAsync()
        {
            _tcs.TrySetResult(_exit); // no-op if Cancel() already faulted it
            return _tcs.Task;
        }
        public void Cancel()
        {
            _owner.CancelCalled = true;
            _tcs.TrySetException(new System.OperationCanceledException());
        }
    }
}

internal sealed class FakeManifestSource : IBinaryManifestSource
{
    public BinaryManifest? ManifestToReturn;
    public Task<BinaryManifest> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ManifestToReturn!);
}

internal sealed class ScriptedProcessRunner : IProcessRunner
{
    public ProcessResult NextResult = new(0, "1.2.3", "");
    public Task<ProcessResult> RunAsync(string command, IReadOnlyList<string> args, CancellationToken cancellationToken = default)
        => Task.FromResult(NextResult);
}

public class SandboxOrchestratorTests
{
    private static SandboxRunRequest SampleRequest()
    {
        SandboxRunRequest.TryCreate("owner/repo", 1, out var req, out _);
        return req!;
    }

    private static SandboxOrchestrator Build(
        out FakeVersionGate versionGate, out FakePrereqValidator prereq, out FakeClusterCreator cluster,
        out FakeSandboxCli cli, out FakeArgvSecretGuard secret, out FakeProvisionRunner provision)
    {
        versionGate = new FakeVersionGate();
        prereq = new FakePrereqValidator();
        cluster = new FakeClusterCreator();
        cli = new FakeSandboxCli();
        secret = new FakeArgvSecretGuard();
        provision = new FakeProvisionRunner();
        return new SandboxOrchestrator(versionGate, prereq, cluster, cli, provision, secret);
    }

    [Fact]
    public async Task AC6_Orchestration_OrdersStages_AndSucceeds_OnHappyPath()
    {
        var orch = Build(out var vg, out var pr, out var cl, out var cli, out var sg, out var prov);
        prov.StdoutToEmit = new[] { "{\"event\":\"start\"}" };
        prov.StderrToEmit = new[] { "prose warning" };
        var sink = new RecordingSink();

        var result = await orch.RunAsync(SampleRequest(), sink);

        Assert.True(result.Success);
        Assert.Equal("minikube", result.KubeContext);
        var order = result.Steps.ConvertAll(s => s.Stage);
        Assert.Equal(new[]
        {
            OrchestrationStage.VersionGate,
            OrchestrationStage.Prerequisites,
            OrchestrationStage.Cluster,
            OrchestrationStage.Check,
            OrchestrationStage.Provision,
        }, order);
        // AC3 — channels routed correctly.
        Assert.Equal(new[] { "{\"event\":\"start\"}" }, sink.StdoutLines);
        Assert.Equal(new[] { "prose warning" }, sink.StderrLines);
    }

    [Fact]
    public async Task AC4_VersionGateFails_AbortsBeforeProvision()
    {
        var orch = Build(out var vg, out _, out _, out _, out _, out var prov);
        vg.Supported = false;

        var result = await orch.RunAsync(SampleRequest(), new RecordingSink());

        Assert.False(result.Success);
        Assert.Equal(OrchestrationStage.VersionGate, result.FailedStage);
        Assert.False(prov.Started);
    }

    [Fact]
    public async Task AC1_CheckNotReady_AbortsBeforeProvision()
    {
        var orch = Build(out _, out _, out _, out var cli, out _, out var prov);
        cli.ExitCodeToReturn = 10; // not ready
        cli.Stderr = new[] { "docker not running" };

        var result = await orch.RunAsync(SampleRequest(), new RecordingSink());

        Assert.False(result.Success);
        Assert.Equal(OrchestrationStage.Check, result.FailedStage);
        Assert.Contains("docker not running", result.Errors);
        Assert.False(prov.Started);
    }

    [Fact]
    public async Task AC5_SecretOnArgv_RefusesBeforeLaunch()
    {
        var orch = Build(out _, out _, out _, out _, out var sg, out var prov);
        sg.RefusalToReturn = "secret detected";

        var result = await orch.RunAsync(SampleRequest(), new RecordingSink());

        Assert.False(result.Success);
        Assert.Equal(OrchestrationStage.Provision, result.FailedStage);
        Assert.Contains("secret detected", result.Errors);
        Assert.False(prov.Started);
    }

    [Fact]
    public async Task AC2_Provision_CanBeCancelled()
    {
        var orch = Build(out _, out _, out _, out _, out _, out var prov);
        prov.ExitToReturn = 0;
        var cts = new CancellationTokenSource();
        cts.Cancel(); // already cancelled -> session.Cancel() fires synchronously during WaitAsync registration

        var result = await orch.RunAsync(SampleRequest(), new RecordingSink(), cts.Token);

        Assert.False(result.Success);
        Assert.Equal(OrchestrationStage.Cancelled, result.FailedStage);
        Assert.True(prov.CancelCalled);
    }

    [Fact]
    public async Task ClusterFailure_AbortsBeforeProvision()
    {
        var orch = Build(out _, out _, out var cl, out _, out _, out var prov);
        cl.Success = false;

        var result = await orch.RunAsync(SampleRequest(), new RecordingSink());

        Assert.False(result.Success);
        Assert.Equal(OrchestrationStage.Cluster, result.FailedStage);
        Assert.False(prov.Started);
    }
}

public class ManifestPrereqValidatorTests
{
    private static BinaryEntry RequiredEntry(string constraint) => new()
    {
        Name = "minikube",
        Required = true,
        VersionConstraint = constraint,
        Probe = new ProbeSpec { Command = "minikube", Args = new[] { "version" } },
    };

    [Fact]
    public async Task PresentBinary_Satisfies()
    {
        var manifest = new BinaryManifest { Binaries = new[] { RequiredEntry(">=1.0.0") } };
        var source = new FakeManifestSource { ManifestToReturn = manifest };
        var validator = new ManifestPrereqValidator(source, new BinaryVerifier(new ScriptedProcessRunner { NextResult = new ProcessResult(0, "1.2.3", "") }));

        var result = await validator.CheckAsync();

        Assert.True(result.Satisfied);
    }

    [Fact]
    public async Task VersionMismatch_Fails()
    {
        var manifest = new BinaryManifest { Binaries = new[] { RequiredEntry(">=2.0.0") } };
        var source = new FakeManifestSource { ManifestToReturn = manifest };
        var validator = new ManifestPrereqValidator(source, new BinaryVerifier(new ScriptedProcessRunner { NextResult = new ProcessResult(0, "1.2.3", "") }));

        var result = await validator.CheckAsync();

        Assert.False(result.Satisfied);
        Assert.NotEmpty(result.Errors);
    }
}

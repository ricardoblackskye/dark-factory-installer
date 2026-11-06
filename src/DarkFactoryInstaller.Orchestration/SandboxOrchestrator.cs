using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Ordered sandbox pipeline (issue #7): version gate -&gt; prereqs -&gt; cluster -&gt; check -&gt; provision.
/// Every dependency is an interface so the orchestrator is testable independently of the UI (AC6).
/// </summary>
public sealed class SandboxOrchestrator : ISandboxOrchestrator
{
    private readonly IVersionGate _versionGate;
    private readonly IPrerequisiteValidator _prereqValidator;
    private readonly IClusterCreator _clusterCreator;
    private readonly ISandboxCli _sandboxCli;
    private readonly IProvisionRunner _provisionRunner;
    private readonly IArgvSecretGuard _secretGuard;

    public SandboxOrchestrator(
        IVersionGate versionGate,
        IPrerequisiteValidator prereqValidator,
        IClusterCreator clusterCreator,
        ISandboxCli sandboxCli,
        IProvisionRunner provisionRunner,
        IArgvSecretGuard secretGuard)
    {
        _versionGate = versionGate;
        _prereqValidator = prereqValidator;
        _clusterCreator = clusterCreator;
        _sandboxCli = sandboxCli;
        _provisionRunner = provisionRunner;
        _secretGuard = secretGuard;
    }

    public async Task<OrchestrationResult> RunAsync(SandboxRunRequest request, IOutputSink sink, CancellationToken cancellationToken = default)
    {
        var steps = new List<OrchestrationStep>();

        // AC4 — version gate first.
        var version = await _versionGate.CheckAsync(cancellationToken).ConfigureAwait(false);
        steps.Add(new OrchestrationStep(OrchestrationStage.VersionGate, version.Supported, version.Errors));
        if (!version.Supported)
        {
            return OrchestrationResult.Failure(OrchestrationStage.VersionGate, version.Errors, steps);
        }

        // Prereqs (#4/#5): local binaries present and version-ok.
        var prereq = await _prereqValidator.CheckAsync(cancellationToken).ConfigureAwait(false);
        steps.Add(new OrchestrationStep(OrchestrationStage.Prerequisites, prereq.Satisfied, prereq.Errors));
        if (!prereq.Satisfied)
        {
            return OrchestrationResult.Failure(OrchestrationStage.Prerequisites, prereq.Errors, steps);
        }

        // Cluster (#6): create the target cluster and capture its kube context.
        string? kubeContext = null;
        var cluster = await _clusterCreator.CreateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        steps.Add(new OrchestrationStep(OrchestrationStage.Cluster, cluster.Success, cluster.Errors));
        if (!cluster.Success)
        {
            return OrchestrationResult.Failure(OrchestrationStage.Cluster, cluster.Errors, steps, kubeContext);
        }
        kubeContext = cluster.KubeContext;

        // AC1 — run the CLI preflight; do not provision if it reports not-ready.
        CheckRunResult check;
        try
        {
            check = await _sandboxCli.RunCheckAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            var checkErrs = new[] { $"Pre-flight check failed: {ex.Message}" };
            steps.Add(new OrchestrationStep(OrchestrationStage.Check, false, checkErrs));
            return OrchestrationResult.Failure(OrchestrationStage.Check, checkErrs, steps, kubeContext);
        }

        var ready = check.ExitCode == 0;
        var checkErrors = ready ? System.Array.Empty<string>() : check.StderrLines;
        steps.Add(new OrchestrationStep(OrchestrationStage.Check, ready, checkErrors));
        if (!ready)
        {
            return OrchestrationResult.Failure(OrchestrationStage.Check, checkErrors, steps, kubeContext);
        }

        // AC5 — refuse secrets on argv before launching the provision.
        var commandLine = CliRunner.BuildCommand(request, kubeContext);
        var refusal = _secretGuard.RefuseIfSecretPresent(commandLine);
        if (refusal is not null)
        {
            steps.Add(new OrchestrationStep(OrchestrationStage.Provision, false, new[] { refusal }));
            return OrchestrationResult.Failure(OrchestrationStage.Provision, new[] { refusal }, steps, kubeContext);
        }

        // AC2/AC3 — launch the provision as a streaming, cancellable subprocess.
        IProvisionSession session;
        try
        {
            session = await _provisionRunner.StartAsync(request, kubeContext, sink, cancellationToken).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            var startErrs = new[] { $"Failed to start provision: {ex.Message}" };
            steps.Add(new OrchestrationStep(OrchestrationStage.Provision, false, startErrs));
            return OrchestrationResult.Failure(OrchestrationStage.Provision, startErrs, steps, kubeContext);
        }

        using var cancelReg = cancellationToken.Register(() => session.Cancel());
        int exitCode;
        try
        {
            exitCode = await session.WaitAsync().ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            steps.Add(new OrchestrationStep(OrchestrationStage.Provision, false, new[] { "Provision was cancelled." }));
            return OrchestrationResult.Cancelled(steps, kubeContext);
        }

        var provisionSucceeded = exitCode == 0;
        steps.Add(new OrchestrationStep(
            OrchestrationStage.Provision,
            provisionSucceeded,
            provisionSucceeded ? System.Array.Empty<string>() : new[] { $"Provision exited with code {exitCode}." }));

        if (!provisionSucceeded)
        {
            return OrchestrationResult.Failure(OrchestrationStage.Provision, new[] { $"Provision exited with code {exitCode}." }, steps, kubeContext);
        }

        return OrchestrationResult.Success(kubeContext, exitCode, steps);
    }
}

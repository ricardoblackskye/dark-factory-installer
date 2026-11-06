using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>A running provision process the orchestrator can observe and cancel (issue #7, AC2).</summary>
public interface IProvisionSession
{
    /// <summary>The exit code of the provision process (completes when the process exits).</summary>
    Task<int> WaitAsync();

    /// <summary>Kills the process (best-effort) so the orchestrator can cancel (AC2).</summary>
    void Cancel();
}

/// <summary>
/// Launches the real <c>df:sandbox-up</c> provision as a streaming, cancellable subprocess
/// (issue #7, AC2/AC3). The orchestrator depends on this interface so it is testable without a real process.
/// </summary>
public interface IProvisionRunner
{
    /// <summary>
    /// Starts the provision, wiring stdout -&gt; <paramref name="sink"/>.OnStdout and stderr -&gt;
    /// <paramref name="sink"/>.OnStderr. Returns a session the caller can wait on / cancel.
    /// </summary>
    Task<IProvisionSession> StartAsync(SandboxRunRequest request, string? kubeContext, IOutputSink sink, CancellationToken cancellationToken);
}

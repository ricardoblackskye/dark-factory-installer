using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Owns the ordered sandbox pipeline (version gate -&gt; prereqs -&gt; cluster -&gt; check -&gt; provision) and is
/// testable independently of the UI (issue #7, AC6).
/// </summary>
public interface ISandboxOrchestrator
{
    Task<OrchestrationResult> RunAsync(SandboxRunRequest request, IOutputSink sink, CancellationToken cancellationToken = default);
}

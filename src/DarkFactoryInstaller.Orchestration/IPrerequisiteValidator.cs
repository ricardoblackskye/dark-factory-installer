namespace DarkFactoryInstaller.Orchestration;

/// <summary>Result of local-binary prerequisite verification (issue #7, prereqs stage).</summary>
public sealed record PrereqCheckResult(bool Satisfied, IReadOnlyList<string> Errors);

/// <summary>
/// Verifies the local binaries the CLI depends on are present and version-ok (#4/#5), independently of the
/// UI so the orchestrator stays unit-testable (issue #7, AC6).
/// </summary>
public interface IPrerequisiteValidator
{
    Task<PrereqCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}

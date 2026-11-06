using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Abstraction over running agent-eve's headless <c>df:sandbox-up</c> so the check logic is
/// unit-testable without a real process (issues #4 / #7).
/// </summary>
public interface ISandboxCli
{
    /// <summary>Runs <c>df:sandbox-up --check</c> and returns the captured output.</summary>
    Task<CheckRunResult> RunCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Output of a CLI run: process exit code plus captured stdout (NDJSON) and stderr (prose) lines.
/// </summary>
public sealed record CheckRunResult(
    int ExitCode,
    IReadOnlyList<string> StdoutLines,
    IReadOnlyList<string> StderrLines);

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Runs a process and surfaces its stdout/stderr <em>line by line</em> as they arrive, so the UI can
/// render progress live (issue #8, AC2/AC5). Mirrors the injectable-seam style of #4/#5/#7.
/// Returns the accumulated result for parity with <see cref="IProcessRunner"/>.
/// </summary>
public interface IStreamingProcessRunner
{
    Task<ProcessResult> RunAsync(
        string command,
        IReadOnlyList<string> args,
        Action<string> onStdoutLine,
        Action<string> onStderrLine,
        CancellationToken ct = default);
}
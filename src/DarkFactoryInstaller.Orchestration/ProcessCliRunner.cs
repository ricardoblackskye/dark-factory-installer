using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Runs <c>df:sandbox-up --check</c> as a real subprocess and captures its NDJSON stdout and prose
/// stderr. The CLI emits the prerequisite <c>problems</c> (code/subject/fix) to stderr as prose and a
/// terminal NDJSON <c>done</c> event carrying the exit code — so we branch on the exit code and read
/// remediation from stderr (per the #582 / #603 contract).
/// </summary>
public sealed class ProcessCliRunner : ISandboxCli
{
    public async Task<CheckRunResult> RunCheckAsync(CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "df:sandbox-up",
            Arguments = "--check",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        if (!process.Start())
        {
            return new CheckRunResult(-1, System.Array.Empty<string>(), new[] { "Could not start df:sandbox-up --check." });
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new CheckRunResult(
            process.ExitCode,
            SplitLines(stdout.ToString()),
            SplitLines(stderr.ToString()));
    }

    private static IReadOnlyList<string> SplitLines(string text)
        => text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
}

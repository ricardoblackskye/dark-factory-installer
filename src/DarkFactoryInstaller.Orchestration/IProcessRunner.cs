using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>Result of running a probe command.</summary>
public sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);

/// <summary>Runs a command + args, capturing stdout/stderr (injectable for tests). Issue #5.</summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string command, IReadOnlyList<string> args, CancellationToken cancellationToken = default);
}

/// <summary>Real process runner used by the app; mirrors <see cref="ProcessCliRunner"/>.</summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string command, IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        if (!process.Start())
            return new ProcessResult(-1, "", $"Could not start {command}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}

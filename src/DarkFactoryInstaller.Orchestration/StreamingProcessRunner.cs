using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>Real <see cref="IStreamingProcessRunner"/> over <see cref="Process"/> (issue #8).</summary>
public sealed class StreamingProcessRunner : IStreamingProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string command,
        IReadOnlyList<string> args,
        Action<string> onStdoutLine,
        Action<string> onStderrLine,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stdout.AppendLine(e.Data);
            onStdoutLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stderr.AppendLine(e.Data);
            onStderrLine(e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        return new ProcessResult(proc.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
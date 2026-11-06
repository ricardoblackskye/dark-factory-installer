using System.Diagnostics;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Real <see cref="IProvisionRunner"/> that launches <c>df:sandbox-up</c> via a streaming, cancellable
/// process. stdout is forwarded line-by-line to <see cref="IOutputSink.OnStdout"/> and stderr to
/// <see cref="IOutputSink.OnStderr"/> (issue #7, AC2/AC3).
/// </summary>
public sealed class ProcessProvisionRunner : IProvisionRunner
{
    public async Task<IProvisionSession> StartAsync(SandboxRunRequest request, string? kubeContext, IOutputSink sink, CancellationToken cancellationToken)
    {
        var command = CliRunner.BuildCommand(request, kubeContext);
        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var exe = tokens[0];
        var args = tokens.Length > 1 ? string.Join(" ", tokens, 1, tokens.Length - 1) : string.Empty;

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdoutDone = new TaskCompletionSource<bool>();
        var stderrDone = new TaskCompletionSource<bool>();
        var exitTcs = new TaskCompletionSource<int>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                sink.OnStdout(e.Data);
            }
            else
            {
                stdoutDone.TrySetResult(true);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                sink.OnStderr(e.Data);
            }
            else
            {
                stderrDone.TrySetResult(true);
            }
        };
        process.Exited += (_, _) => exitTcs.TrySetResult(process.ExitCode);

        if (!process.Start())
        {
            process.Dispose();
            throw new System.InvalidOperationException($"Failed to start provision process: {command}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return new ProcessProvisionSession(process, exitTcs.Task, stdoutDone.Task, stderrDone.Task);
    }

    private sealed class ProcessProvisionSession : IProvisionSession
    {
        private readonly Process _process;
        private readonly Task<int> _exitTask;
        private readonly Task _stdoutDone;
        private readonly Task _stderrDone;

        public ProcessProvisionSession(Process process, Task<int> exitTask, Task stdoutDone, Task stderrDone)
        {
            _process = process;
            _exitTask = exitTask;
            _stdoutDone = stdoutDone;
            _stderrDone = stderrDone;
        }

        public async Task<int> WaitAsync()
        {
            var code = await _exitTask.ConfigureAwait(false);
            await Task.WhenAll(_stdoutDone, _stderrDone).ConfigureAwait(false);
            try { _process.Dispose(); } catch { }
            return code;
        }

        public void Cancel()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch
            {
                // best-effort: process may already be gone
            }
        }
    }
}

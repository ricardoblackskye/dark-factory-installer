using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Builds the command line that drives agent-eve's headless <c>df:sandbox-up</c>
/// for a validated <see cref="SandboxRunRequest"/>. The UI layer calls this to
/// launch a sandbox run (provisioning itself lives in agent-eve, per #584).
/// </summary>
public static class CliRunner
{
    /// <summary>
    /// Returns the full command string, e.g.
    /// <c>df:sandbox-up --repo owner/repo --issue 42</c>.
    /// </summary>
    public static string BuildCommand(SandboxRunRequest request)
    {
        // TDD RED: stub — real command building lands in GREEN.
        throw new System.NotImplementedException("CliRunner.BuildCommand not implemented (RED)");
    }
}

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
    /// Returns the full command string for a provision run, e.g.
    /// <c>df:sandbox-up --repo owner/repo --issue 42</c>.
    /// </summary>
    public static string BuildCommand(SandboxRunRequest request)
        => $"df:sandbox-up --repo {request.Repo} --issue {request.Issue}";

    /// <summary>
    /// Returns the command string for the read-only prerequisite check
    /// (<c>df:sandbox-up --check</c>), used by the prerequisite detector (#4).
    /// </summary>
    public static string BuildCheckCommand()
        => "df:sandbox-up --check";
}

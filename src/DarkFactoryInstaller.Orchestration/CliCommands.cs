using System.Collections.Generic;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Builds the headless CLI commands the installer invokes (issue #9). The CLI is an npm script, so the
/// installer wraps it as `npm run &lt;script&gt; -- &lt;flags&gt;`. `--workspace-dir` aligns the CLI's own
/// idempotent skip state with the installer's target (AC3).
/// </summary>
public static class CliCommands
{
    public static (string Exe, IReadOnlyList<string> Args) Up(string repo, int issue, string workspaceDir)
    {
        var args = new List<string>
        {
            "run", "df:sandbox-up", "--",
            "--workspace-dir", workspaceDir,
            "--repo", repo,
            "--issue", issue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return ("npm", args);
    }

    public static (string Exe, IReadOnlyList<string> Args) Down(bool deleteCluster)
    {
        var args = new List<string> { "run", "df:sandbox-down", "--" };
        if (deleteCluster) args.Add("--delete-cluster");
        return ("npm", args);
    }
}
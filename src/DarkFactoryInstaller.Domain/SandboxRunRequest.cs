namespace DarkFactoryInstaller.Domain;

/// <summary>
/// A request to run the Dark Factory sandbox via agent-eve's headless
/// <c>df:sandbox-up</c> (see #582 / #585 / #603). This is the Domain model the
/// Orchestration layer turns into a CLI invocation.
/// </summary>
public sealed record SandboxRunRequest(string Repo, int Issue)
{
    /// <summary>
    /// Validates and constructs a <see cref="SandboxRunRequest"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> and <paramref name="request"/> on success; <c>false</c> with a
    /// human-readable <paramref name="error"/> otherwise.
    /// </returns>
    public static bool TryCreate(string repo, int issue, out SandboxRunRequest? request, out string? error)
    {
        request = null;
        error = null;

        if (string.IsNullOrWhiteSpace(repo) ||
            !System.Text.RegularExpressions.Regex.IsMatch(repo, @"^[\w.-]+/[\w.-]+$"))
        {
            error = "repo must be in 'owner/repo' form (letters, digits, '.', '-' allowed).";
            return false;
        }

        if (issue <= 0)
        {
            error = "issue must be a positive integer.";
            return false;
        }

        request = new SandboxRunRequest(repo, issue);
        return true;
    }

    /// <summary>
    /// Optional minimum CLI version (SemVer constraint string); the orchestrator refuses to provision if
    /// the CLI reports an older version (issue #7, AC4). Interpreted by <c>CliVersionGate</c>.
    /// </summary>
    public string? MinimumCliVersion { get; init; } = null;
}

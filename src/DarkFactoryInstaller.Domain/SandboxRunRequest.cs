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
        // TDD RED: stub — real validation lands in GREEN.
        throw new System.NotImplementedException("SandboxRunRequest.TryCreate not implemented (RED)");
    }
}

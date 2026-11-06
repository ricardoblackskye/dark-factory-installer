namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Refuses to launch if a secret would be passed on argv (issue #7, AC5; mirrors the #582 exit-2 contract where
/// the CLI itself refuses token/password/api-key/secret on the command line).
/// </summary>
public interface IArgvSecretGuard
{
    /// <summary>Returns <c>null</c> if the command line is safe, or a refusal reason if a secret pattern is detected.</summary>
    string? RefuseIfSecretPresent(string commandLine);
}

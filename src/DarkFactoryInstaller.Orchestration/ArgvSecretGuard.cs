using System.Text.RegularExpressions;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Scans the built command line for secret-like tokens and refuses if found (issue #7, AC5). This is the
/// app-side fail-closed backstop that mirrors the CLI's own exit-2 refusal of secrets on argv.
/// </summary>
public sealed class ArgvSecretGuard : IArgvSecretGuard
{
    private static readonly string[] SecretKeywords = { "token", "password", "passwd", "pwd", "api-key", "apikey", "apikeys", "secret", "api_key" };
    private static readonly Regex SecretValuePattern = new(@"[/\-\s](token|password|passwd|pwd|api[-_]?key|secret)[=\s]+([^\s""']+)", RegexOptions.IgnoreCase);

    public string? RefuseIfSecretPresent(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var lowered = commandLine.ToLowerInvariant();
        foreach (var keyword in SecretKeywords)
        {
            if (lowered.Contains(keyword))
            {
                return $"Refusing to launch: a secret-like token ('{keyword}') was found on the command line. Supply credentials via a vault handle/stdin per the #582 contract.";
            }
        }

        var match = SecretValuePattern.Match(commandLine);
        if (match.Success)
        {
            return $"Refusing to launch: a secret value ('{match.Groups[2].Value}') was detected on the command line. Supply credentials via a vault handle/stdin per the #582 contract.";
        }

        return null;
    }
}

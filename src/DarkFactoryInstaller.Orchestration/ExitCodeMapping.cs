using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Maps a process exit code to a user-facing message + suggested action + category (issue #10, AC1).
/// The installer invokes the CLI via `npm run`, so the observed exit code is the CLI's own.
/// </summary>
public sealed record ExitCodeInfo(string Message, string SuggestedAction, ErrorCategory Category);

public static class ExitCodeMapping
{
    public static ExitCodeInfo? Map(int exitCode) => exitCode switch
    {
        0 => null, // success — not an error
        2 => new ExitCodeInfo(
            "The CLI refused a secret on the command line.",
            "Remove any --token/--password/--api-key/--secret and use a vault handle, then retry.",
            ErrorCategory.OperatorFixable),
        10 => new ExitCodeInfo(
            "Prerequisites are not satisfied (exit 10).",
            "Run the prerequisite check and fix the listed tools, then retry.",
            ErrorCategory.OperatorFixable),
        _ => new ExitCodeInfo(
            $"An unexpected error occurred (exit {exitCode}).",
            "This looks like an internal fault. Please report it using the diagnostics below.",
            ErrorCategory.Internal),
    };
}

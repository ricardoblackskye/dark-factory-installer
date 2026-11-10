using DarkFactoryInstaller.Domain;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Runs the CLI version command and compares the detected version against the declared minimum
/// (issue #7 AC4). Wired into the live UI path for issue #24. The CLI exposes its version via
/// `df:sandbox-up --version` (agent-eve #582 T1), which prints the package semver to stdout.
/// </summary>
public sealed class CliVersionGate : IVersionGate
{
    private const string VersionCommand = "npm";
    private static readonly IReadOnlyList<string> VersionArgs = new[] { "run", "df:sandbox-up", "--", "--version" };

    private readonly IProcessRunner _processRunner;
    private readonly string _minimumVersion;

    public CliVersionGate(IProcessRunner processRunner, string minimumVersion)
    {
        _processRunner = processRunner;
        _minimumVersion = minimumVersion ?? string.Empty;
    }

    public async Task<VersionGateResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_minimumVersion))
        {
            return VersionGateResult.Ok(null);
        }

        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(VersionCommand, VersionArgs, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return VersionGateResult.Unsupported(null, new[] { $"Could not determine CLI version: {ex.Message}" });
        }

        var detected = SemVer.ExtractFirst(result.Stdout + "\n" + result.Stderr);
        if (detected is null)
        {
            return VersionGateResult.Unsupported(null, new[] { $"Could not parse CLI version from output: {result.Stdout.Trim()}" });
        }

        var constraint = VersionConstraint.Parse(_minimumVersion);
        if (!constraint.IsSatisfied(detected.Value))
        {
            return VersionGateResult.Unsupported(detected.Value.ToString(), new[] { $"CLI version {detected.Value} does not satisfy {_minimumVersion}." });
        }

        return VersionGateResult.Ok(detected.Value.ToString());
    }
}

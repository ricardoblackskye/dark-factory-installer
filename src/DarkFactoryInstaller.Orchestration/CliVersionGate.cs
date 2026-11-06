using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Runs the CLI version command and compares the detected version against
/// <see cref="SandboxRunRequest.MinimumCliVersion"/> (issue #7, AC4).
/// </summary>
public sealed class CliVersionGate : IVersionGate
{
    private const string VersionCommand = "df:sandbox-up";
    private const string VersionArgs = "--version"; // TODO: confirm exact flag against #582 contract during wiring

    private readonly IProcessRunner _processRunner;
    private readonly SandboxRunRequest _request;

    public CliVersionGate(IProcessRunner processRunner, SandboxRunRequest request)
    {
        _processRunner = processRunner;
        _request = request;
    }

    public async Task<VersionGateResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_request.MinimumCliVersion))
        {
            return VersionGateResult.Ok(null);
        }

        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(VersionCommand, new[] { VersionArgs }, cancellationToken).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            return VersionGateResult.Unsupported(null, new[] { $"Could not determine CLI version: {ex.Message}" });
        }

        var detected = SemVer.ExtractFirst(result.Stdout + "\n" + result.Stderr);
        if (detected is null)
        {
            return VersionGateResult.Unsupported(null, new[] { $"Could not parse CLI version from output: {result.Stdout.Trim()}" });
        }

        var constraint = VersionConstraint.Parse(_request.MinimumCliVersion!);
        if (!constraint.IsSatisfied(detected.Value))
        {
            return VersionGateResult.Unsupported(detected.Value.ToString(), new[] { $"CLI version {detected.Value} does not satisfy {_request.MinimumCliVersion}." });
        }

        return VersionGateResult.Ok(detected.Value.ToString());
    }
}

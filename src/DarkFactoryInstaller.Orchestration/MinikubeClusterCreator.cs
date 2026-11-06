using System.Text.RegularExpressions;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Creates/tears down a local minikube cluster via <see cref="IProcessRunner"/> (issue #6). The kube
/// context is parsed from <c>minikube start</c> stdout ("kubectl context will be "ctx"") and falls back
/// to the profile name. Failures are returned as <see cref="ClusterCreationResult"/> / <see cref="ClusterTeardownResult"/>
/// carrying the stderr problems — never thrown as a raw crash (AC3).
/// </summary>
public sealed class MinikubeClusterCreator : IClusterCreator
{
    private const string DefaultProfile = "minikube";
    private static readonly Regex ContextRegex = new("\"([^\"]+)\"");

    private readonly IProcessRunner _processRunner;

    public MinikubeClusterCreator(IProcessRunner processRunner) => _processRunner = processRunner;

    public async Task<ClusterCreationResult> CreateAsync(string? profile = null, CancellationToken cancellationToken = default)
    {
        var effectiveProfile = string.IsNullOrWhiteSpace(profile) ? DefaultProfile : profile!;
        try
        {
            var result = await _processRunner
                .RunAsync("minikube", new[] { "start", "-p", effectiveProfile }, cancellationToken)
                .ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                return ClusterCreationResult.Failure(effectiveProfile, SplitProblems(result.Stderr));
            }

            var kubeContext = ParseKubeContext(result.Stdout) ?? effectiveProfile;
            return new ClusterCreationResult(true, effectiveProfile, kubeContext, ClusterState.Created, System.Array.Empty<string>());
        }
        catch (System.Exception ex)
        {
            return ClusterCreationResult.Failure(effectiveProfile, new[] { $"Could not start minikube: {ex.Message}" });
        }
    }

    public async Task<ClusterTeardownResult> TeardownAsync(string profile, ClusterTeardownPreference preference, CancellationToken cancellationToken = default)
    {
        var command = preference == ClusterTeardownPreference.TearDown ? "delete" : "stop";
        try
        {
            var result = await _processRunner
                .RunAsync("minikube", new[] { command, "-p", profile }, cancellationToken)
                .ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                return new ClusterTeardownResult(false, SplitProblems(result.Stderr));
            }

            return new ClusterTeardownResult(true, System.Array.Empty<string>());
        }
        catch (System.Exception ex)
        {
            return new ClusterTeardownResult(false, new[] { $"Could not {command} minikube: {ex.Message}" });
        }
    }

    private static string? ParseKubeContext(string stdout)
    {
        // minikube prints a line like:  * kubectl context will be "darkfactory"
        var idx = stdout.IndexOf("kubectl context will be", System.StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var match = ContextRegex.Match(stdout.Substring(idx));
        return match.Success ? match.Groups[1].Value : null;
    }

    private static IReadOnlyList<string> SplitProblems(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new[] { "minikube reported a failure with no details." };
        }

        return text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
    }
}

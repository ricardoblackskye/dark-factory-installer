using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Detects whether the operator's machine satisfies the prerequisites the headless CLI needs, by
/// delegating to <c>df:sandbox-up --check</c> (issue #4).
///
/// The CLI reports a missing/mismatched prerequisite with exit code 10 and writes the actionable
/// <c>problems</c> (subject + fix) to stderr as prose; stdout carries NDJSON events. We branch on the
/// exit code and surface the per-tool state by scanning the prose for each known tool, so we never parse
/// the NDJSON for status (#582 contract: NDJSON is the event stream, prose carries remediation).
/// </summary>
public sealed class PrerequisiteChecker
{
    private static readonly (string Keyword, string Name)[] Tools =
    {
        ("docker", "Docker"),
        ("kubectl", "kubectl"),
        ("helm", "Helm"),
        ("python", "Python"),
        ("minikube", "minikube"),
    };

    private readonly ISandboxCli _cli;

    public PrerequisiteChecker(ISandboxCli cli) => _cli = cli;

    public async Task<PrerequisiteCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        CheckRunResult run;
        try
        {
            run = await _cli.RunCheckAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            return new PrerequisiteCheckResult(
                false,
                System.Array.Empty<PrerequisiteStatus>(),
                new[] { $"Could not run df:sandbox-up --check: {ex.Message}" });
        }

        // The CLI writes "ERROR: <line>" per problem (subject then fix). Strip the prefix.
        var problems = run.StderrLines
            .Select(l => l.StartsWith("ERROR: ", System.StringComparison.OrdinalIgnoreCase)
                ? l.Substring("ERROR: ".Length)
                : l)
            .Where(l => l.Length > 0)
            .ToArray();

        var statuses = new List<PrerequisiteStatus>();
        foreach (var (keyword, name) in Tools)
        {
            bool required = name != "minikube"; // minikube is optional (cluster path only)
            var matches = problems.Where(p => p.Contains(keyword, System.StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0)
            {
                statuses.Add(new PrerequisiteStatus(name, required, true, null, null, null, null));
            }
            else
            {
                statuses.Add(new PrerequisiteStatus(name, required, false, null, null, matches[0], "prerequisites-missing"));
            }
        }

        bool ready = run.ExitCode == 0 && !statuses.Any(s => !s.Present);
        return new PrerequisiteCheckResult(ready, statuses, problems);
    }
}

using System.Collections.Generic;
using System.Linq;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Turns a process result and/or the <c>done</c> event's problems into a classified <see cref="ErrorReport"/>
/// (issue #10, AC1/AC2/AC3). Operator-fixable codes point at the environment; unknown codes and bare
/// non-zero exits are treated as internal and carry secret-free diagnostics.
/// </summary>
public static class ErrorClassifier
{
    // Codes that represent an operator-actionable, environment problem (issue #10, Q2).
    private static readonly HashSet<ProvisionProblemCode> OperatorFixableCodes = new()
    {
        ProvisionProblemCode.PrerequisitesMissing,
        ProvisionProblemCode.PrerequisitesMismatch,
        ProvisionProblemCode.ArtifactUnresolvable,
        ProvisionProblemCode.ClusterUnreachable,
        ProvisionProblemCode.ProofFailed,
        ProvisionProblemCode.TeardownFailed,
    };

    public static ErrorReport? Classify(
        ProcessResult result,
        IReadOnlyList<ProvisionProblem>? problems = null,
        string? lastStep = null,
        string? lastPhase = null)
    {
        // problems from the done event take precedence over the bare exit code
        if (problems is { Count: > 0 })
        {
            var actions = problems.Select(p => ProblemMessageMapper.ToMessage(p.Code)).ToArray();
            var category = problems.Any(p => !OperatorFixableCodes.Contains(p.Code))
                ? ErrorCategory.Internal
                : ErrorCategory.OperatorFixable;
            var headline = actions.Length == 1 ? actions[0] : "Provisioning failed with multiple problems.";
            var diagnostics = category == ErrorCategory.Internal
                ? BuildDiagnostics(result.ExitCode, lastStep, lastPhase)
                : null;
            return new ErrorReport(category, headline, actions, diagnostics);
        }

        if (result.ExitCode == 0)
            return null; // success — nothing to report

        var info = ExitCodeMapping.Map(result.ExitCode);
        if (info is null) return null;
        var diag = info.Category == ErrorCategory.Internal
            ? BuildDiagnostics(result.ExitCode, lastStep, lastPhase)
            : null;
        return new ErrorReport(info.Category, info.Message, new[] { info.SuggestedAction }, diag);
    }

    private static string BuildDiagnostics(int exitCode, string? lastStep, string? lastPhase) =>
        $"exit={exitCode}; step={lastStep ?? "n/a"}; phase={lastPhase ?? "n/a"}; ts={System.DateTime.UtcNow:o}";
}

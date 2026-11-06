using System.Collections.Generic;

namespace DarkFactoryInstaller.Domain;

/// <summary>
/// Readiness of a single prerequisite tool the headless CLI needs, as surfaced by
/// <c>df:sandbox-up --check</c>. See issue #4.
/// </summary>
public sealed record PrerequisiteStatus(
    string Name,
    bool Required,
    bool Present,
    string? DetectedVersion,
    string? RequiredConstraint,
    string? Message,
    string? Code);

/// <summary>
/// Result of a prerequisite check: overall readiness plus a per-tool breakdown and the raw
/// problem lines the CLI emitted, so the UI can show the exact remediation text from #603.
/// </summary>
public sealed record PrerequisiteCheckResult(
    bool Ready,
    IReadOnlyList<PrerequisiteStatus> Statuses,
    IReadOnlyList<string> RawProblems);

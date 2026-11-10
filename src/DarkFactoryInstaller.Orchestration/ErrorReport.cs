using System.Collections.Generic;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// A classified, user-facing error (issue #10, AC3). <see cref="Diagnostics"/> is populated only for
/// internal errors and contains secret-free context for a bug report.
/// </summary>
public sealed record ErrorReport(
    ErrorCategory Category,
    string Headline,
    IReadOnlyList<string> Actions,
    string? Diagnostics);

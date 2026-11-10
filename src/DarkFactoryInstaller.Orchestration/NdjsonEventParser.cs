using System;
using System.Collections.Generic;
using System.Text.Json;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Parses one NDJSON line from the headless CLI into a <see cref="ProvisionEvent"/> (issue #8, AC1/AC6).
/// A malformed / non-JSON / contract-violating line returns <c>null</c> and is reported to
/// <paramref name="onMalformed"/> rather than thrown — the UI must never crash on a bad line.
/// Unknown fields are ignored; unknown step/phase values are treated as unparseable.
/// </summary>
public sealed class NdjsonEventParser
{
    private readonly Action<string>? _onMalformed;

    public NdjsonEventParser(Action<string>? onMalformed = null) => _onMalformed = onMalformed;

    // agent-eve emits hyphenated problem codes (e.g. "prerequisites-missing"); C# enum members
    // cannot, so map the wire string to the enum.
    private static readonly Dictionary<string, ProvisionProblemCode> CodeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["prerequisites-missing"] = ProvisionProblemCode.PrerequisitesMissing,
        ["prerequisites-mismatch"] = ProvisionProblemCode.PrerequisitesMismatch,
        ["artifact-unresolvable"] = ProvisionProblemCode.ArtifactUnresolvable,
        ["cluster-unreachable"] = ProvisionProblemCode.ClusterUnreachable,
        ["proof-failed"] = ProvisionProblemCode.ProofFailed,
        ["teardown-failed"] = ProvisionProblemCode.TeardownFailed,
        ["internal"] = ProvisionProblemCode.Internal,
    };

    public ProvisionEvent? ParseLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line!);
        }
        catch (JsonException ex)
        {
            _onMalformed?.Invoke($"malformed NDJSON (not JSON): {ex.Message} :: {line}");
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("step", out var stepEl) || stepEl.ValueKind != JsonValueKind.String) return Fail(line!);
            if (!root.TryGetProperty("phase", out var phaseEl) || phaseEl.ValueKind != JsonValueKind.String) return Fail(line!);
            if (!root.TryGetProperty("message", out var msgEl) || msgEl.ValueKind != JsonValueKind.String) return Fail(line!);
            if (!Enum.TryParse<ProvisionStep>(stepEl.GetString(), true, out var step)) return Fail(line!);
            if (!Enum.TryParse<ProvisionPhase>(phaseEl.GetString(), true, out var phase)) return Fail(line!);

            var ev = new ProvisionEvent
            {
                Ts = root.TryGetProperty("ts", out var ts) && ts.ValueKind == JsonValueKind.String ? ts.GetString()! : string.Empty,
                Step = step,
                Phase = phase,
                Message = msgEl.GetString()!,
                ExitCode = root.TryGetProperty("exitCode", out var ec) && ec.ValueKind == JsonValueKind.Number ? ec.GetInt32() : null,
            };

            if (root.TryGetProperty("problems", out var pEl) && pEl.ValueKind == JsonValueKind.Array)
            {
                var problems = new List<ProvisionProblem>();
                foreach (var p in pEl.EnumerateArray())
                {
                    if (p.ValueKind != JsonValueKind.Object) continue;
                    if (!p.TryGetProperty("code", out var c) || c.ValueKind != JsonValueKind.String) continue;
                    if (!CodeMap.TryGetValue(c.GetString()!, out var pc)) continue;
                    var subject = p.TryGetProperty("subject", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                    var fix = p.TryGetProperty("fix", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
                    problems.Add(new ProvisionProblem { Code = pc, Subject = subject, Fix = fix });
                }

                if (problems.Count > 0) ev = ev with { Problems = problems };
            }

            return ev;
        }
    }

    private ProvisionEvent? Fail(string line)
    {
        _onMalformed?.Invoke($"unparseable NDJSON event (contract violation): {line}");
        return null;
    }
}
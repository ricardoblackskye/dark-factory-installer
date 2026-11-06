using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DarkFactoryInstaller.Domain;

/// <summary>
/// A single binary the headless CLI needs, as declared by agent-eve's
/// <c>artifacts.lock.json</c> manifest (#603). The manifest is the single source of truth; this
/// model is just its deserialised shape. See issue #5.
/// </summary>
public sealed class BinaryEntry
{
    public string Name { get; set; } = "";
    public string VersionConstraint { get; set; } = "";
    public bool Required { get; set; }
    public string? RequiredFor { get; set; }
    public string AcquiredBy { get; set; } = "operator";
    public ProbeSpec? Probe { get; set; }
    public string? Fix { get; set; }
    public string? Source { get; set; }
    public string? ExpectedDigest { get; set; }
}

/// <summary>How to detect an installed binary's version (command + args), from the manifest.</summary>
public sealed class ProbeSpec
{
    public string Command { get; set; } = "";
    public IReadOnlyList<string> Args { get; set; } = System.Array.Empty<string>();
}

/// <summary>The <c>binaries</c> array of the agent-eve manifest (other manifest sections are ignored).</summary>
public sealed class BinaryManifest
{
    public IReadOnlyList<BinaryEntry> Binaries { get; set; } = System.Array.Empty<BinaryEntry>();
}

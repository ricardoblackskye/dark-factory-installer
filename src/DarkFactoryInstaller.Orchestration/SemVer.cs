using System;
using System.Text.RegularExpressions;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>Minimal semantic version (major.minor.patch) with comparison. Issue #5.</summary>
public readonly struct SemVer : IComparable<SemVer>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    public SemVer(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>Extracts the first <c>major.minor.patch</c> token from arbitrary tool output.</summary>
    public static SemVer? ExtractFirst(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var m = Regex.Match(text, @"\d+\.\d+\.\d+");
        return m.Success ? Parse(m.Value) : null;
    }

    public static SemVer Parse(string s)
    {
        var parts = s.Split('.');
        var major = int.Parse(parts[0]);
        var minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        var patch = parts.Length > 2 ? int.Parse(parts[2]) : 0;
        return new SemVer(major, minor, patch);
    }

    public int CompareTo(SemVer other)
    {
        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        return Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

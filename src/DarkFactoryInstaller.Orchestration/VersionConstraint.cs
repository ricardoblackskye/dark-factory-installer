namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// A version constraint of the form <c>[op]major.minor.patch</c> (default operator <c>&gt;=</c>).
/// Supports &gt;=, &gt;, ==, &lt;=, &lt;. Issue #5.
/// </summary>
public sealed class VersionConstraint
{
    public string Operator { get; }
    public SemVer Version { get; }

    private VersionConstraint(string op, SemVer version)
    {
        Operator = op;
        Version = version;
    }

    public static VersionConstraint Parse(string s)
    {
        s = s.Trim();
        foreach (var op in new[] { ">=", "<=", "==", ">", "<" })
        {
            if (s.StartsWith(op, StringComparison.Ordinal))
                return new VersionConstraint(op, SemVer.Parse(s.Substring(op.Length).Trim()));
        }

        return new VersionConstraint(">=", SemVer.Parse(s));
    }

    public bool IsSatisfied(SemVer detected)
    {
        var cmp = detected.CompareTo(Version);
        return Operator switch
        {
            ">=" => cmp >= 0,
            ">" => cmp > 0,
            "<=" => cmp <= 0,
            "<" => cmp < 0,
            "==" => cmp == 0,
            _ => cmp >= 0,
        };
    }
}

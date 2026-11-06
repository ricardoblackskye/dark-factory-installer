using System.Text.Json;
using System.Text.RegularExpressions;
using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

/// <summary>
/// Documentation drift guards (issue #16). These run in the standard <c>dotnet test</c> CI job and
/// FAIL THE BUILD when the docs drift from the code/manifest they describe. Mirrors agent-eve's #238
/// doc-drift-guard pattern: each guard is a pure check returning drift errors, and every guard has a
/// mutation test proving it fires on synthetic drift (AC5) so the guards are not vacuous.
///
/// Convention (ADR-0004): documentation changes accompany the code they describe, in the same PR.
/// Drift fails CI; it does not auto-open a PR and we do not commit generated snapshots.
/// </summary>
public class DocDriftGuards
{
    private static readonly string RepoRoot = FindRepoRoot();

    // Repo root is found by walking up from the test assembly to the solution file, so the guards
    // work both locally (dotnet test) and on CI (windows-latest), where the DLL sits under bin/.
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DarkFactoryInstaller.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate repo root: no DarkFactoryInstaller.sln found walking up from " + AppContext.BaseDirectory);
        }

        return dir.FullName;
    }

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    // G1 (AC1/AC4a): every ADR is linked from the index (no orphan ADRs).
    internal static List<string> CheckNoOrphanAdrs(string indexText, IReadOnlyList<string> adrFiles)
    {
        var errors = new List<string>();
        foreach (var adr in adrFiles)
        {
            // adr is a repo-relative path like "docs/adr/0002-....md"; the link target is the file name.
            var fileName = Path.GetFileName(adr);
            var pattern = @"\]\([^)]*" + Regex.Escape(fileName) + @"\)";
            if (!Regex.IsMatch(indexText, pattern))
            {
                errors.Add($"ADR '{fileName}' is not linked from docs/adr/README.md (orphan ADR).");
            }
        }

        return errors;
    }

    // G2 (AC4b): no dead internal links in docs/**.md.
    internal static List<string> CheckNoDeadInternalLinks(IReadOnlyList<(string FilePath, string Content)> docs)
    {
        var errors = new List<string>();
        var linkRegex = new Regex(@"\[[^\]]*\]\(([^)]+)\)");
        foreach (var (filePath, content) in docs)
        {
            var baseDir = Path.GetDirectoryName(filePath)!;
            foreach (Match m in linkRegex.Matches(content))
            {
                var target = m.Groups[1].Value.Trim();
                if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("#"))
                {
                    continue;
                }

                var pathOnly = target.Split('#')[0];
                if (string.IsNullOrEmpty(pathOnly) || pathOnly.EndsWith("/"))
                {
                    continue; // anchor-only or directory-style link
                }

                if (!Uri.TryCreate(pathOnly, UriKind.Relative, out _))
                {
                    continue;
                }

                var resolved = Path.GetFullPath(Path.Combine(baseDir, pathOnly));
                if (!File.Exists(resolved))
                {
                    errors.Add($"Dead internal link in '{filePath}': '{target}' does not resolve to a file.");
                }
            }
        }

        return errors;
    }

    // G3 (AC4c): README documents both command forms CliRunner emits.
    internal static List<string> CheckReadmeDocumentsCli(string readmeText)
    {
        var errors = new List<string>();

        var checkCmd = CliRunner.BuildCheckCommand();
        if (!readmeText.Contains(checkCmd))
        {
            errors.Add($"README.md does not document '{checkCmd}' (CliRunner.BuildCheckCommand()).");
        }

        SandboxRunRequest.TryCreate("owner/repo", 1, out var req, out var err);
        if (req is null)
        {
            throw new InvalidOperationException("DocDriftGuards sample request failed: " + err);
        }

        var runCmd = CliRunner.BuildCommand(req);
        if (!readmeText.Contains(runCmd))
        {
            errors.Add($"README.md does not document '{runCmd}' (CliRunner.BuildCommand()).");
        }

        return errors;
    }

    // G4 (AC4d): docs/prerequisites.md enumerates every binary + versionConstraint from the manifest,
    // parsed via the Domain BinaryManifest model (same options as FileBinaryManifestSource).
    internal static List<string> CheckPrerequisitesDocMatchesManifest(string prereqDoc, string manifestText)
    {
        var errors = new List<string>();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var manifest = JsonSerializer.Deserialize<BinaryManifest>(manifestText, options);
        if (manifest?.Binaries is null)
        {
            return errors;
        }

        foreach (var binary in manifest.Binaries)
        {
            if (!prereqDoc.Contains(binary.Name))
            {
                errors.Add($"docs/prerequisites.md does not list the binary '{binary.Name}' from the manifest.");
            }

            if (!string.IsNullOrEmpty(binary.VersionConstraint) && !prereqDoc.Contains(binary.VersionConstraint))
            {
                errors.Add(
                    $"docs/prerequisites.md does not list the version constraint '{binary.VersionConstraint}' for '{binary.Name}'.");
            }
        }

        return errors;
    }

    // ===================== Guard tests (real repo files) =====================

    [Fact]
    public void AC1_G1_NoOrphanAdrs()
    {
        var adrDir = Path.Combine(RepoRoot, "docs", "adr");
        var adrFiles = Directory.GetFiles(adrDir, "*.md")
            .Where(f => !string.Equals(Path.GetFileName(f), "README.md", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(RepoRoot, f).Replace('\\', '/'))
            .ToList();

        var errors = CheckNoOrphanAdrs(ReadRepoFile("docs/adr/README.md"), adrFiles);
        Assert.Empty(errors);
    }

    [Fact]
    public void AC4b_G2_NoDeadInternalDocLinks()
    {
        var docsDir = Path.Combine(RepoRoot, "docs");
        var docs = Directory.GetFiles(docsDir, "*.md", SearchOption.AllDirectories)
            .Select(f => (FilePath: f, Content: File.ReadAllText(f)))
            .ToList();

        var errors = CheckNoDeadInternalLinks(docs);
        Assert.Empty(errors);
    }

    [Fact]
    public void AC4c_G3_ReadmeDocumentsCli()
    {
        var errors = CheckReadmeDocumentsCli(ReadRepoFile("README.md"));
        Assert.Empty(errors);
    }

    [Fact]
    public void AC4d_G4_PrerequisitesDocMatchesManifest()
    {
        var errors = CheckPrerequisitesDocMatchesManifest(
            ReadRepoFile("docs/prerequisites.md"),
            ReadRepoFile("src/DarkFactoryInstaller/resources/artifacts.lock.json"));
        Assert.Empty(errors);
    }

    // ===================== Mutation tests (AC5): prove each guard fires =====================

    [Fact]
    public void Mutation_OrphanAdr_ReportsDrift()
    {
        // Index links only 0001; 0002 is an unlinked (orphan) ADR.
        var index = "See [ADR 0001](0001-framework-choice.md).";
        var adrFiles = new[]
        {
            "docs/adr/0001-framework-choice.md",
            "docs/adr/0002-prereq-delegates-to-cli.md"
        };

        var errors = CheckNoOrphanAdrs(index, adrFiles);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Mutation_DeadLink_ReportsDrift()
    {
        var docs = new List<(string, string)>
        {
            ("docs/adr/README.md", "Broken [link](does-not-exist.md).")
        };

        var errors = CheckNoDeadInternalLinks(docs);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Mutation_MissingCliCommand_ReportsDrift()
    {
        var readme = "We drive df:sandbox-up but never show the command.";

        var errors = CheckReadmeDocumentsCli(readme);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Mutation_MissingBinary_ReportsDrift()
    {
        // Doc lists four binaries; the manifest (below) declares five — minikube is missing.
        var prereqDoc = "docker >=20.10.0\nkubectl >=1.28.0\nhelm >=3.14.0\npython >=3.10.0\n";
        var manifest = "{\"binaries\":["
            + "{\"name\":\"docker\",\"versionConstraint\":\">=20.10.0\"},"
            + "{\"name\":\"kubectl\",\"versionConstraint\":\">=1.28.0\"},"
            + "{\"name\":\"helm\",\"versionConstraint\":\">=3.14.0\"},"
            + "{\"name\":\"python\",\"versionConstraint\":\">=3.10.0\"},"
            + "{\"name\":\"minikube\",\"versionConstraint\":\">=1.30.0\"}]}";

        var errors = CheckPrerequisitesDocMatchesManifest(prereqDoc, manifest);
        Assert.NotEmpty(errors);
    }
}

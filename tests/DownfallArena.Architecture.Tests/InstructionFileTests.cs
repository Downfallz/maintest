using System.Text;

namespace DownfallArena.Architecture.Tests;

/// <summary>
/// The instruction files agents load, held to the shape ADR 0181 gives them: the root <c>AGENTS.md</c> an index
/// under a budget, each area's instructions imported by the <c>CLAUDE.md</c> beside them, and every plan listed
/// in <c>docs/README.md</c>.
/// </summary>
public sealed class InstructionFileTests
{
    private const int MaximumLines = 200;
    private const int MaximumBytes = 12 * 1024;
    private const int MaximumLineLength = 300;

    private static readonly string[] Areas =
    [
        "table",
        "battle",
        "studio",
        "learning",
        "infra",
        "src/DownfallArena.Cli",
    ];

    private static readonly string Root = RepositoryFiles.Root;

    [Fact]
    public void Root_agents_file_stays_under_two_hundred_lines()
    {
        var lines = File.ReadAllLines(Path.Combine(Root, "AGENTS.md"));

        lines.Length.ShouldBeLessThan(MaximumLines, "AGENTS.md is an index (ADR 0181): move detail where it belongs");
    }

    [Fact]
    public void Root_agents_file_stays_under_twelve_kilobytes()
    {
        var bytes = File.ReadAllBytes(Path.Combine(Root, "AGENTS.md"));

        bytes.Length.ShouldBeLessThan(MaximumBytes, "AGENTS.md is an index (ADR 0181): move detail where it belongs");
    }

    [Fact]
    public void Root_agents_file_has_no_line_over_three_hundred_characters()
    {
        var longLines = File.ReadAllLines(Path.Combine(Root, "AGENTS.md"), Encoding.UTF8)
            .Select((line, index) => (Number: index + 1, line.Length))
            .Where(line => line.Length > MaximumLineLength)
            .Select(line => $"line {line.Number}: {line.Length} characters");

        longLines.ShouldBeEmpty();
    }

    [Fact]
    public void Each_area_has_an_agents_file()
    {
        var missing = Areas.Where(area => !File.Exists(Path.Combine(Root, area, "AGENTS.md")));

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void Each_area_claude_file_imports_the_agents_file_beside_it()
    {
        var notImporting = Areas.Where(area =>
        {
            var claude = Path.Combine(Root, area, "CLAUDE.md");
            return !File.Exists(claude) || File.ReadAllText(claude).Trim() != "@AGENTS.md";
        });

        notImporting.ShouldBeEmpty();
    }

    [Fact]
    public void Every_plan_under_docs_is_listed_in_the_docs_index()
    {
        var docs = Path.Combine(Root, "docs");
        var index = File.ReadAllText(Path.Combine(docs, "README.md"));

        var unlisted = Directory.EnumerateFiles(docs, "*plan*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(docs, path).Replace('\\', '/'))
            // A journal entry is named after its title, which may say "plan" without being one (ADR 0183).
            .Where(relative => !relative.StartsWith("learning/journal/", StringComparison.Ordinal))
            .Where(relative => !index.Contains($"({relative})", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        unlisted.ShouldBeEmpty();
    }
}

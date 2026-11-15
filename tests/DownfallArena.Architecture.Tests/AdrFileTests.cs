using System.Text.RegularExpressions;

namespace DownfallArena.Architecture.Tests;

/// <summary>
/// The decision records, held to the shape that lets the directory be its own index (ADR 0183): one number per
/// file, the number and the title on the first line, the status on a line of its own, and no table to edit.
/// </summary>
public sealed partial class AdrFileTests
{
    private const string Template = "0000-template.md";

    private static readonly string Directory = RepositoryFiles.PathTo("docs", "adr");

    [Fact]
    public void No_number_is_taken_by_two_records()
    {
        var taken = FindNumbersTakenTwice(Records().Select(Path.GetFileName)!);

        taken.ShouldBeEmpty("an ADR number is taken once: rename the later record to the next number (ADR 0183)");
    }

    [Fact]
    public void A_number_two_files_share_is_reported_with_both_files()
    {
        var taken = FindNumbersTakenTwice(["0007-one.md", "0008-two.md", "0008-three.md", "0009-four.md"]);

        taken.ShouldBe(["0008: 0008-three.md, 0008-two.md"]);
    }

    [Fact]
    public void Every_record_is_named_by_a_number_and_a_slug()
    {
        var misnamed = System.IO.Directory.EnumerateFiles(Directory, "*.md")
            .Select(Path.GetFileName)
            .Where(name => name != "README.md" && !RecordName().IsMatch(name!))
            .Order(StringComparer.Ordinal);

        misnamed.ShouldBeEmpty();
    }

    [Fact]
    public void Every_record_opens_with_its_own_number_and_a_title()
    {
        var wrong = Records()
            .Where(path => !File.ReadLines(path).First().StartsWith($"# {Number(path)}. ", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal);

        wrong.ShouldBeEmpty("the first line of an ADR is '# NNNN. Title', with the number of its file");
    }

    [Fact]
    public void Every_record_states_its_status_above_its_first_section()
    {
        var silent = Records()
            .Where(path => !File.ReadLines(path)
                .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
                .Any(line => line.StartsWith("Status: ", StringComparison.Ordinal) && line.Length > "Status: ".Length))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal);

        silent.ShouldBeEmpty("an ADR says 'Status: ...' on a line of its own, above '## Context'");
    }

    [Fact]
    public void The_readme_keeps_no_table_of_records()
    {
        var rows = File.ReadLines(Path.Combine(Directory, "README.md"))
            .Where(line => RecordLink().IsMatch(line));

        rows.ShouldBeEmpty("the directory is the index (ADR 0183): a list every pull request edits conflicts");
    }

    private static IEnumerable<string> Records() =>
        System.IO.Directory.EnumerateFiles(Directory, "*.md")
            .Where(path => RecordName().IsMatch(Path.GetFileName(path)) && Path.GetFileName(path) != Template);

    private static string Number(string path) => Path.GetFileName(path)[..4];

    private static string[] FindNumbersTakenTwice(IEnumerable<string> names) =>
        [.. names
            .GroupBy(name => name[..4], StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Order(StringComparer.Ordinal))}")];

    [GeneratedRegex(@"^\d{4}-[a-z0-9]+(-[a-z0-9]+)*\.md$")]
    private static partial Regex RecordName();

    [GeneratedRegex(@"\]\(\d{4}-[^)]*\.md\)")]
    private static partial Regex RecordLink();
}

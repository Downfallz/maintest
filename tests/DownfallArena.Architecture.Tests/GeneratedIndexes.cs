using System.Globalization;
using System.Text;

namespace DownfallArena.Architecture.Tests;

/// <summary>
/// The two indexes nobody writes by hand (ADR 0184): the table of decision records and the list of journal
/// entries, each built from the files it lists. The tests compare what is committed with what is built here, and
/// write it instead when <c>UPDATE_INDEXES=1</c>, so one implementation both generates and checks.
/// </summary>
internal static class GeneratedIndexes
{
    public const string Command = "UPDATE_INDEXES=1 dotnet test --project tests/DownfallArena.Architecture.Tests";
    public const string Start = "<!-- index:start (generated, do not edit) -->";
    public const string End = "<!-- index:end -->";

    public static bool Writing => Environment.GetEnvironmentVariable("UPDATE_INDEXES") == "1";

    /// <summary>One row per record: its number, the title of its first line, its whole status paragraph.</summary>
    public static string AdrTable(IEnumerable<string> records)
    {
        var table = new StringBuilder("| # | Title | Status |\n| --- | --- | --- |\n");
        foreach (var path in records.OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);
            var lines = File.ReadAllLines(path);
            var title = lines[0][(lines[0].IndexOf(". ", StringComparison.Ordinal) + 2)..];
            table.Append(CultureInfo.InvariantCulture, $"| [{name[..4]}]({name}) | {Cell(title)} | {Cell(Status(lines))} |\n");
        }

        return table.ToString();
    }

    /// <summary>The journal's entries, newest first, each under its first line.</summary>
    public static string JournalList(IEnumerable<string> entries)
    {
        var list = new StringBuilder();
        foreach (var path in entries.OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
        {
            list.Append(CultureInfo.InvariantCulture, $"- [{File.ReadLines(path).First()[2..]}]({Path.GetFileName(path)})\n");
        }

        return list.ToString();
    }

    /// <summary>The text of <paramref name="page"/> with <paramref name="index"/> between its two markers.</summary>
    public static string Place(string page, string index)
    {
        var start = page.IndexOf(Start, StringComparison.Ordinal);
        var end = page.IndexOf(End, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            throw new InvalidOperationException($"The page has lost its index markers: '{Start}' then '{End}'.");
        }

        return $"{page[..(start + Start.Length)]}\n\n{index}\n{page[end..]}";
    }

    private static string Status(string[] lines)
    {
        var first = Array.FindIndex(lines, line => line.StartsWith("Status: ", StringComparison.Ordinal));
        if (first < 0)
        {
            return string.Empty;
        }

        // A status may run over several lines: it ends at the blank line, the date, the template's Rulebook line or
        // the section after it.
        var paragraph = lines.Skip(first).TakeWhile((line, index) => index == 0 || (line.Length > 0
            && !line.StartsWith("Date:", StringComparison.Ordinal)
            && !line.StartsWith("Rulebook:", StringComparison.Ordinal)
            && !line.StartsWith('#')));
        return string.Join(' ', paragraph.Select(line => line.Trim()))["Status: ".Length..];
    }

    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);
}

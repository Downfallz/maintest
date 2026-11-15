using System.Text.RegularExpressions;

namespace DownfallArena.Architecture.Tests;

/// <summary>
/// The learning journal, one file per entry (ADR 0183): each named by its date, its order in the day and a slug,
/// each opening with that date and a title, none written back into the page that introduces them, and all
/// listed by a README generated from them.
/// </summary>
public sealed partial class JournalFileTests
{
    private static readonly string Page = RepositoryFiles.PathTo("docs", "learning", "journal.md");
    private const string Readme = "README.md";

    private static readonly string Directory = RepositoryFiles.PathTo("docs", "learning", "journal");

    [Fact]
    public void The_journal_has_entries()
    {
        Entries().ShouldNotBeEmpty();
    }

    [Fact]
    public void Every_entry_is_named_by_its_date_its_order_in_the_day_and_a_slug()
    {
        var misnamed = Entries()
            .Select(Path.GetFileName)
            .Where(name => !EntryName().IsMatch(name!))
            .Order(StringComparer.Ordinal);

        misnamed.ShouldBeEmpty("a journal entry is 'YYYY-MM-DD-NN-slug.md' (docs/learning/journal.md)");
    }

    [Fact]
    public void Every_entry_opens_with_the_date_of_its_file_and_a_title()
    {
        var wrong = Entries()
            .Where(path => !File.ReadLines(path).First()
                .StartsWith($"# {Path.GetFileName(path)[..10]}. ", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal);

        wrong.ShouldBeEmpty("the first line of a journal entry is '# YYYY-MM-DD. Title', with the date of its file");
    }

    [Fact]
    public void The_journal_page_holds_no_entry()
    {
        var entries = File.ReadLines(Page).Where(line => DatedHeading().IsMatch(line));

        entries.ShouldBeEmpty("an entry is a file under docs/learning/journal/ (ADR 0183), not a section of the page");
    }

    [Fact]
    public void The_journal_readme_lists_every_entry_newest_first()
    {
        var readme = Path.Combine(Directory, Readme);
        var committed = File.ReadAllText(readme);
        var generated = GeneratedIndexes.Place(committed, GeneratedIndexes.JournalList(Entries()));

        if (GeneratedIndexes.Writing)
        {
            File.WriteAllText(readme, generated);
        }

        File.ReadAllText(readme).ShouldBe(generated, $"the journal index is generated, never edited: run `{GeneratedIndexes.Command}`");
    }

    private static IEnumerable<string> Entries() =>
        System.IO.Directory.EnumerateFiles(Directory, "*.md").Where(path => Path.GetFileName(path) != Readme);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}-\d{2}-[a-z0-9]+(-[a-z0-9]+)*\.md$")]
    private static partial Regex EntryName();

    [GeneratedRegex(@"^#+ \d{4}-\d{2}-\d{2}")]
    private static partial Regex DatedHeading();
}

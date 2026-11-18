using DownfallArena.Infrastructure.Matches.Scenarios;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The match scenarios the repository ships (docs/tabletop/scenarios-plan.md, S7): every <c>*.json</c> of the
/// <c>scenarios/</c> directory, of its <c>tutorials/</c> (ADR 0187) and of its <c>bugs/</c> (docs/tabletop/feedback-plan.md,
/// R6), read once when the host starts, each under an id made of its file's name. They are
/// the repository's, not this host's: the admin panel lists and plays them, and nothing at the host removes one or
/// lists it for players. A file that is not a scenario, or that does not carry its content, is said on the console and
/// left out.
/// </summary>
internal sealed class RepositoryScenarios
{
    /// <summary>Where the scenarios are read from, relative to where the host runs, as its pages are.</summary>
    public const string DefaultDirectory = "scenarios";

    /// <summary>What every shipped scenario's id begins with: a scenario's id, which no cut one has.</summary>
    public const string Prefix = "sc-repo-";

    /// <summary>The directory of <c>scenarios/</c> the tutorials are kept in, and what a tutorial's id says after the prefix.</summary>
    public const string TutorialsDirectory = "tutorials";

    /// <summary>What a tutorial's id begins with, which no other shipped scenario's does.</summary>
    public const string TutorialPrefix = Prefix + "tutorial-";

    /// <summary>The directory of <c>scenarios/</c> the bug scenarios are kept in, each named <c>&lt;issue&gt;-&lt;slug&gt;.json</c>.</summary>
    public const string BugsDirectory = "bugs";

    /// <summary>What a bug scenario's id begins with, which no other shipped scenario's does.</summary>
    public const string BugPrefix = Prefix + "bug-";

    /// <summary>The longest id a scenario's routes take is 63 characters.</summary>
    private const int LongestName = 63 - 8;

    private readonly Dictionary<string, Shipped> _byId;

    private RepositoryScenarios(IReadOnlyList<Shipped> all)
    {
        All = all;
        Tutorials = [.. all.Where(one => one.Document.Tutorial is not null).OrderBy(one => one.Document.Tutorial!.Order)];
        _byId = all.ToDictionary(one => one.Id, StringComparer.Ordinal);
    }

    /// <summary>A scenario the repository ships: its id, the file it is, and what it holds.</summary>
    internal sealed record Shipped(string Id, string File, ScenarioDocument Document)
    {
        /// <summary>The issue a bug scenario reproduces; none for any other.</summary>
        public int? Bug => BugOf(File);
    }

    public static RepositoryScenarios None { get; } = new([]);

    /// <summary>Every scenario shipped, in the order of their files' names.</summary>
    public IReadOnlyList<Shipped> All { get; }

    /// <summary>The scenarios of <c>tutorials/</c> that carry a lesson (ADR 0187), in the order of the series.</summary>
    public IReadOnlyList<Shipped> Tutorials { get; }

    public Shipped? Get(string? id) => id is not null && _byId.TryGetValue(id, out var shipped) ? shipped : null;

    /// <summary>The scenarios of a directory; none for a directory that is not there.</summary>
    public static RepositoryScenarios Read(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return None;
        }

        var all = new List<Shipped>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            Add(all, path, Path.GetFileName(path), IdOf(Path.GetFileName(path)), Kind.Plain);
        }

        var tutorials = Path.Combine(directory, TutorialsDirectory);
        if (Directory.Exists(tutorials))
        {
            foreach (var path in Directory.EnumerateFiles(tutorials, "*.json").Order(StringComparer.Ordinal))
            {
                Add(all, path, $"{TutorialsDirectory}/{Path.GetFileName(path)}", IdOf($"tutorial-{Path.GetFileName(path)}"), Kind.Tutorial);
            }
        }

        var bugs = Path.Combine(directory, BugsDirectory);
        if (Directory.Exists(bugs))
        {
            foreach (var path in Directory.EnumerateFiles(bugs, "*.json").Order(StringComparer.Ordinal))
            {
                Add(all, path, $"{BugsDirectory}/{Path.GetFileName(path)}", IdOf($"bug-{Path.GetFileName(path)}"), Kind.Bug);
            }
        }

        return new RepositoryScenarios(all);
    }

    /// <summary>
    /// One file read into <paramref name="all" />, or said on the console and left out: another file has its id, it
    /// is not a scenario, it does not carry its content, it is a tutorial outside the tutorials' directory or named
    /// as one there, another tutorial already has its place in the series, or it is a bug's not named after its issue
    /// or teaching a lesson.
    /// </summary>
    private static void Add(List<Shipped> all, string path, string file, string id, Kind kind)
    {
        var tutorial = kind == Kind.Tutorial;
        if (kind == Kind.Plain && id.StartsWith(TutorialPrefix, StringComparison.Ordinal))
        {
            Console.WriteLine($"  Scenario file {file} is left out: the id {id} is a tutorial's, and it is not under {TutorialsDirectory}/.");
            return;
        }

        if (kind == Kind.Plain && id.StartsWith(BugPrefix, StringComparison.Ordinal))
        {
            Console.WriteLine($"  Scenario file {file} is left out: the id {id} belongs to a bug scenario, but the file is not under {BugsDirectory}/.");
            return;
        }

        if (kind == Kind.Bug && BugOf(file) is null)
        {
            Console.WriteLine($"  Scenario file {file} is left out: a bug scenario is named after its issue, <number>-<slug>.json.");
            return;
        }

        if (all.Exists(one => one.Id == id))
        {
            Console.WriteLine($"  Scenario file {file} is left out: another file already has the id {id}.");
            return;
        }

        try
        {
            var document = ScenarioFile.Read(File.ReadAllText(path));
            if (document.Schema is null)
            {
                // It would play only while this host's content is the one it was cut on, and stop at the next change.
                Console.WriteLine($"  Scenario file {file} is left out: it does not carry its content (scenario-generate does, and scenario-cut --with-content).");
                return;
            }

            if (document.Tutorial is not null && kind == Kind.Plain)
            {
                // Only the tutorials' directory is held step by step by the tests; a lesson elsewhere would be offered unchecked.
                Console.WriteLine($"  Scenario file {file} is left out: it has a tutorial block, and a tutorial is kept under {TutorialsDirectory}/.");
                return;
            }

            if (document.Tutorial is not null && kind == Kind.Bug)
            {
                Console.WriteLine($"  Scenario file {file} is left out: it is under {BugsDirectory}/ and has a tutorial block.");
                return;
            }

            if (document.Tutorial is null && tutorial)
            {
                Console.WriteLine($"  Scenario file {file} is left out: it is under {TutorialsDirectory}/ and has no tutorial block.");
                return;
            }

            if (document.Tutorial is { } lesson && all.Find(one => one.Document.Tutorial?.Order == lesson.Order) is { } taken)
            {
                Console.WriteLine($"  Scenario file {file} is left out: {taken.File} is already tutorial {lesson.Order}.");
                return;
            }

            all.Add(new Shipped(id, file, document));
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Console.WriteLine($"  Scenario file {file} is left out: {failure.Message}");
        }
    }

    /// <summary>
    /// The issue a shipped file reproduces: the number a file under <c>bugs/</c> is named after, from 1, as
    /// <c>scenario-import</c> writes it; none for any other file.
    /// </summary>
    internal static int? BugOf(string file)
    {
        var prefix = $"{BugsDirectory}/";
        if (!file.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = file[prefix.Length..];
        var digits = name.TakeWhile(char.IsAsciiDigit).Count();
        return digits is > 0 and <= 9 && name.Length > digits && name[digits] == '-' && name[0] != '0'
            && int.TryParse(name.AsSpan(0, digits), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var issue)
            ? issue
            : null;
    }

    private enum Kind
    {
        Plain,
        Tutorial,
        Bug,
    }

    /// <summary>The id a file's scenario is opened by: its name in lower case, whatever is not a letter or a digit a dash.</summary>
    internal static string IdOf(string file)
    {
        var slug = Slug(Path.GetFileNameWithoutExtension(file));
        return Prefix + (slug.Length > LongestName ? slug[..LongestName].TrimEnd('-') : slug.Length == 0 ? "scenario" : slug);
    }

    /// <summary>Text in lower case, whatever is not a letter or a digit a dash, with no dash at either end or twice in a row.</summary>
    internal static string Slug(string text) =>
        string.Join('-', new string([.. text.ToLowerInvariant().Select(one => char.IsAsciiLetterOrDigit(one) ? one : ' ')]).Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

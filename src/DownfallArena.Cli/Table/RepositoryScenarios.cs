using DownfallArena.Infrastructure.Matches.Scenarios;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The match scenarios the repository ships (docs/tabletop/scenarios-plan.md, S7): every <c>*.json</c> of the
/// <c>scenarios/</c> directory, read once when the host starts, each under an id made of its file's name. They are
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

    /// <summary>The longest id a scenario's routes take is 63 characters.</summary>
    private const int LongestName = 63 - 8;

    private readonly Dictionary<string, Shipped> _byId;

    private RepositoryScenarios(IReadOnlyList<Shipped> all)
    {
        All = all;
        _byId = all.ToDictionary(one => one.Id, StringComparer.Ordinal);
    }

    /// <summary>A scenario the repository ships: its id, the file it is, and what it holds.</summary>
    internal sealed record Shipped(string Id, string File, ScenarioDocument Document);

    public static RepositoryScenarios None { get; } = new([]);

    /// <summary>Every scenario shipped, in the order of their files' names.</summary>
    public IReadOnlyList<Shipped> All { get; }

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
            var file = Path.GetFileName(path);
            var id = IdOf(file);
            if (all.Exists(one => one.Id == id))
            {
                Console.WriteLine($"  Scenario file {file} is left out: another file already has the id {id}.");
                continue;
            }

            try
            {
                var document = ScenarioFile.Read(File.ReadAllText(path));
                if (document.Schema is null)
                {
                    // It would play only while this host's content is the one it was cut on, and stop at the next change.
                    Console.WriteLine($"  Scenario file {file} is left out: it does not carry its content (scenario-generate does, and scenario-cut --with-content).");
                    continue;
                }

                all.Add(new Shipped(id, file, document));
            }
            catch (Exception failure) when (failure is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                Console.WriteLine($"  Scenario file {file} is left out: {failure.Message}");
            }
        }

        return new RepositoryScenarios(all);
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

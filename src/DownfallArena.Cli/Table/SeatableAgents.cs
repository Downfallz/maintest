using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The bots a host offers a seat to, read off the weights directory rather than written into a page: the ones
/// <c>learning/seatable.json</c> puts forward, in its order and with its words, then every weights file directly under
/// <c>learning/weights/</c>, by name, as a heuristic. A set searched for another reading sits in a folder named after it
/// (<c>learning/weights/lookahead/</c>) and is not offered here, because played as a heuristic it would be another agent. The admin panel and the pilot both ask the host for this list, so a search that lands a new
/// file is offered the day it lands, and the two pages cannot drift apart on which bot is "the strongest".
/// </summary>
/// <remarks>
/// The host accepts any agent spec it can parse, with or without this list; the list is what a page shows,
/// not what a request may say. A host without the featured file still offers Greedy, Random and the files,
/// so a host run against a stripped checkout has something to seat. The featured file sits beside the weights
/// directory rather than in it, because every file in that directory is a weights file and loaded as one.
/// </remarks>
internal sealed record SeatableAgent(string Value, string Label, bool Featured);

internal static class SeatableAgents
{
    public const string FeaturedFile = "learning/seatable.json";

    private static readonly SeatableAgent[] BuiltIn =
    [
        new("greedy", "Greedy — the baseline", Featured: false),
        new("random", "Random", Featured: false),
    ];

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <param name="weightsDirectory">Where the weights files are, as the path an agent spec names them by (<c>learning/weights</c>).</param>
    /// <param name="featuredFile">The file that puts bots forward, or none.</param>
    public static IReadOnlyList<SeatableAgent> Read(string weightsDirectory, string? featuredFile = FeaturedFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(weightsDirectory);

        var featured = featuredFile is null ? [] : Featured(featuredFile);
        var offered = new List<SeatableAgent>(featured);
        var named = new HashSet<string>(featured.Select(agent => agent.Value), StringComparer.Ordinal);

        foreach (var builtIn in BuiltIn.Where(agent => named.Add(agent.Value)))
        {
            offered.Add(builtIn);
        }

        if (Directory.Exists(weightsDirectory))
        {
            var prefix = weightsDirectory.Replace('\\', '/').TrimEnd('/');
            foreach (var file in Directory.GetFiles(weightsDirectory, "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal))
            {
                var value = $"heuristic:{prefix}/{file}";
                if (named.Add(value))
                {
                    offered.Add(new SeatableAgent(value, Path.GetFileNameWithoutExtension(file)!, Featured: false));
                }
            }
        }

        return offered;
    }

    /// <summary>What the file puts forward, or nothing when there is no file or it says nothing readable.</summary>
    private static List<SeatableAgent> Featured(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var read = JsonSerializer.Deserialize<FeaturedDocument>(File.ReadAllText(path), Options);
            return [.. (read?.Entries ?? [])
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Agent))
                .Select(entry => new SeatableAgent(entry.Agent!.Trim(), string.IsNullOrWhiteSpace(entry.Label) ? entry.Agent.Trim() : entry.Label.Trim(), Featured: true))];
        }
        catch (JsonException)
        {
            // A file that does not parse puts nothing forward; the directory is still offered. A host that
            // refused to start over a label would be a host nobody can open a table on.
            return [];
        }
    }

    private sealed record FeaturedDocument([property: JsonPropertyName("featured")] IReadOnlyList<FeaturedEntry>? Entries);

    private sealed record FeaturedEntry(string? Agent, string? Label);
}

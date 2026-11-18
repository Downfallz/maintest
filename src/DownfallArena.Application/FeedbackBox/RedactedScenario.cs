using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The redacted scenario (docs/tabletop/feedback-plan.md, "The redacted scenario", ADR 0189): the scenario a bug report's
/// <b>Reproduce</b> cut, as it may leave the host for a public issue. <see cref="Of" /> replaces what names the session
/// with the report's own id and keeps the source's answers only up to where the player wrote; <see cref="Check" /> reads
/// the file as written and refuses one that still names the session, keeps a journal line's time, or holds a person.
/// </summary>
public static class RedactedScenario
{
    /// <summary>The top-level field a file carries its content in: the repository's own data, read by anyone.</summary>
    private const string Schema = "schema";

    /// <summary>
    /// The scenario with the report's id where the session's were: its run, and a match id drawn from the report's id, the
    /// same each time. The answers after the cut stop at <paramref name="reportedAt" />, the journal's length when the
    /// player wrote, since what came after is not what they reported; none said keeps them all.
    /// </summary>
    public static ScenarioCut Of(ScenarioCut scenario, string report, int? reportedAt)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(report);
        var after = reportedAt is { } length ? [.. scenario.After.Take(Math.Max(0, length - scenario.Cut.CutAt))] : scenario.After;
        return scenario with { Source = scenario.Source with { Run = report, MatchId = MatchOf(report) }, After = after };
    }

    /// <summary>The match id a redacted scenario is given: drawn from the report's id, so it says nothing of the session's.</summary>
    public static MatchId MatchOf(string report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(report);
        return MatchId.From(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(report)).AsSpan(0, 16)));
    }

    /// <summary>
    /// Whether a written scenario file is redacted: its source's session, run and match the report's own, every journal
    /// line's time at the epoch, and none of <paramref name="names" /> in it as a word of its own, whatever its case, the
    /// content it carries aside. Each name is said by what it is ("an account"), never by itself.
    /// </summary>
    public static Result Check(string file, string report, IEnumerable<(string What, string? Name)> names)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(report);
        ArgumentNullException.ThrowIfNull(names);
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(file);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Refused("The scenario is not a file a scenario reads: it is not offered.");
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.Object)
        {
            return Refused("The scenario is not a file a scenario reads: it is not offered.");
        }

        foreach (var (field, own) in new[] { ("session", report), ("run", report), ("match", MatchOf(report).ToString()) })
        {
            if (!source.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String || value.GetString() != own)
            {
                return Refused($"The scenario's source.{field} is not the report's own: it is not offered.");
            }
        }

        if (!AtTheEpoch(root, "before") || !AtTheEpoch(root, "after"))
        {
            return Refused("A journal line of the scenario keeps its time: it is not offered.");
        }

        var words = Words(root).ToList();
        foreach (var (what, name) in names)
        {
            if (!string.IsNullOrWhiteSpace(name) && words.FirstOrDefault(word => Holds(word.Text, name)) is { Text: not null } held)
            {
                return Refused($"The scenario still holds {what}, in its {held.Where}: it is not offered.");
            }
        }

        return Result.Success();
    }

    private static Result Refused(string message) => Result.Failure(FeedbackErrors.NotRedacted(message));

    /// <summary>Whether every line of a journal the file holds has its time at the epoch, as a scenario file writes them.</summary>
    private static bool AtTheEpoch(JsonElement root, string journal) =>
        !root.TryGetProperty(journal, out var lines) || lines.ValueKind != JsonValueKind.Array || lines.EnumerateArray().All(line =>
            line.ValueKind != JsonValueKind.Object || !line.TryGetProperty("at", out var at)
            || (at.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(at.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var time) && time == DateTimeOffset.UnixEpoch));

    /// <summary>
    /// Every text the file holds, with the top-level field it is in, the content it carries left out. Not its fields'
    /// names, nor the values the format writes from words of its own: a file's format and seat, and a journal line's
    /// kind, slot, speed and who took a seat, when an agent's word names it. A player may well be named <c>person</c>, <c>decision</c> or
    /// <c>player1</c>, and is then looked for in the file's other words alone (#751).
    /// </summary>
    private static IEnumerable<(string Where, string Text)> Words(JsonElement root) =>
        root.EnumerateObject()
            .Where(property => property.Name != Schema && !FormatWords.Contains(property.Name))
            .SelectMany(property => Texts(property.Value, journal: property.Name is "before" or "after").Select(text => (WhereOf(property.Name), text)));

    private static IEnumerable<string> Texts(JsonElement element, bool journal) => element.ValueKind switch
    {
        JsonValueKind.String => [element.GetString()!],
        JsonValueKind.Array => element.EnumerateArray().SelectMany(item => Texts(item, journal)),
        JsonValueKind.Object => element.EnumerateObject()
            .Where(property => !journal || !JournalWords.Contains(property.Name) || (property.Name == "to" && !AgentWord(property.Value)))
            .SelectMany(property => Texts(property.Value, journal)),
        _ => [],
    };

    /// <summary>The top-level fields a scenario file writes from the format's own words.</summary>
    private static readonly HashSet<string> FormatWords = new(["format", "seat"], StringComparer.Ordinal);

    /// <summary>The fields of a journal line written from the format's own words: a kind of line, a seat, a tempo, an agent.</summary>
    private static readonly HashSet<string> JournalWords = new(["kind", "slot", "speed", "to", "late"], StringComparer.Ordinal);

    /// <summary>
    /// Whether who took a seat is named by a word of the host's (<c>person</c>, an agent such as <c>greedy</c>), not by a
    /// path to a weights file the pilot typed, which is looked through as any other text.
    /// </summary>
    private static bool AgentWord(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { } named && !named.Contains('/', StringComparison.Ordinal) && !named.Contains('\\', StringComparison.Ordinal);

    /// <summary>A top-level field as the refusal names it, so the operator sees where the name was found.</summary>
    private static string WhereOf(string field) => field switch
    {
        "before" => "journal before the cut",
        "after" => "journal after the cut",
        _ => field,
    };

    /// <summary>
    /// Whether a text holds a name as a word of its own: neither a letter, a digit, '_' nor '-' on either side. A content
    /// id the journal names (<c>spell:pummel:v1</c>) is read as one word, never as the name inside it.
    /// </summary>
    private static bool Holds(string text, string name) =>
        text.Contains(name, StringComparison.OrdinalIgnoreCase)
        && Regex.IsMatch(ContentIds.Replace(text, " "), $@"(?<![\p{{L}}\p{{N}}_-]){Regex.Escape(name)}(?![\p{{L}}\p{{N}}_-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>A versioned content id, <c>kind:name:vN</c>, as the content and the journal write them.</summary>
    private static readonly Regex ContentIds = new(@"(?<![\p{L}\p{N}_:-])[a-z][a-z0-9-]*:[a-z0-9_-]+:v\d+(?![\p{L}\p{N}_:-])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
}

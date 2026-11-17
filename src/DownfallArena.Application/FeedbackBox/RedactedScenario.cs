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
            if (!string.IsNullOrWhiteSpace(name) && words.Any(word => Holds(word, name)))
            {
                return Refused($"The scenario still holds {what}: it is not offered.");
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

    /// <summary>Every name and text the file holds, the content it carries left out.</summary>
    private static IEnumerable<string> Words(JsonElement root)
    {
        foreach (var property in root.EnumerateObject().Where(property => property.Name != Schema))
        {
            yield return property.Name;
            foreach (var word in Texts(property.Value))
            {
                yield return word;
            }
        }
    }

    private static IEnumerable<string> Texts(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => [element.GetString()!],
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Texts),
        JsonValueKind.Object => element.EnumerateObject().SelectMany(property => Texts(property.Value).Prepend(property.Name)),
        _ => [],
    };

    /// <summary>Whether a text holds a name as a word of its own: neither a letter, a digit, '_' nor '-' on either side.</summary>
    private static bool Holds(string text, string name) =>
        text.Contains(name, StringComparison.OrdinalIgnoreCase)
        && Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}_-]){Regex.Escape(name)}(?![\p{{L}}\p{{N}}_-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
}

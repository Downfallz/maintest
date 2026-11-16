using System.Text.Json;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The levels a player chooses a bot by (ADR 0180): Easy, Medium and Hard, each the name of a bot the host seats, read off
/// <c>learning/seatable.json</c> so a stronger bot behind "Hard" is a change to that file and nothing else. A player is
/// shown the level and never the bot (its spec is a path, and its name means nothing to them); the operator is shown both.
/// </summary>
/// <remarks>
/// A level knows the bots it seated before (<c>formerly</c>), so a game played against last month's "Hard" still says
/// Hard. A bot no level names, which only the operator seats, is shown to a player as "Bot".
/// </remarks>
internal sealed class BotLevels
{
    /// <summary>The level a request that names none is given: the strongest, which is what the lobby seated before levels.</summary>
    public const string Default = "hard";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private BotLevels(IReadOnlyList<BotLevel> all) => All = all;

    /// <summary>Every level, easiest first.</summary>
    public IReadOnlyList<BotLevel> All { get; }

    /// <summary>Easy as a greedy bot playing a random move three times in ten, Medium as Greedy, Hard as <paramref name="strongest" />.</summary>
    public static BotLevels BuiltIn(string strongest) => new(
    [
        new BotLevel("easy", "Easy", "explore:0.3", []),
        new BotLevel("medium", "Medium", "greedy", []),
        new BotLevel("hard", "Hard", strongest, []),
    ]);

    /// <summary>
    /// The levels <paramref name="path" /> names, or the built-in ones where it names none, or is missing or unreadable:
    /// a host that refused to start over a level would be a host nobody can play a bot on.
    /// </summary>
    /// <param name="strongest">What Hard seats when the file does not say: the first bot the file puts forward.</param>
    public static BotLevels Read(string? path, string strongest)
    {
        var builtIn = BuiltIn(strongest);
        if (path is null || !File.Exists(path))
        {
            return builtIn;
        }

        try
        {
            var named = (JsonSerializer.Deserialize<LevelsDocument>(File.ReadAllText(path), Options)?.Levels ?? [])
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Level) && !string.IsNullOrWhiteSpace(entry.Agent))
                .ToDictionary(entry => entry.Level!.Trim().ToLowerInvariant(), StringComparer.Ordinal);
            return new BotLevels([.. builtIn.All.Select(level => named.TryGetValue(level.Level, out var entry)
                ? level with { Agent = entry.Agent!.Trim(), Formerly = [.. (entry.Formerly ?? []).Where(spec => !string.IsNullOrWhiteSpace(spec)).Select(spec => spec.Trim())] }
                : level)]);
        }
        catch (JsonException)
        {
            return builtIn;
        }
    }

    /// <summary>
    /// The levels a process of this host reads, the API and the turn worker alike, so a notice a worker sends names the
    /// bot as the lobby does: <c>learning/seatable.json</c>'s, Hard the first bot it puts forward when it says nothing.
    /// </summary>
    public static BotLevels OfHost(IReadOnlyList<SeatableAgent> agents, string path = SeatableAgents.FeaturedFile) =>
        Read(path, agents.FirstOrDefault(agent => agent.Featured)?.Value ?? "greedy");

    /// <summary>The level of that name (<c>easy</c>, <c>medium</c>, <c>hard</c>), or none.</summary>
    public BotLevel? Named(string? level) =>
        level is null ? null : All.FirstOrDefault(one => string.Equals(one.Level, level.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The level whose bot this is, now or before, or none: a bot only the operator seats.</summary>
    public BotLevel? Of(string? spec) =>
        spec is null ? null : All.FirstOrDefault(level => level.Seats(spec)) ?? All.FirstOrDefault(level => level.Seated(spec));

    /// <summary>
    /// What a player is shown of the bot seated by <paramref name="spec" />: its level ("Hard"), never its name. Every
    /// card and the battle view tag a bot as one beside it, so the word is not repeated.
    /// </summary>
    public string PlayerName(string? spec) => Of(spec)?.Name ?? "Bot";

    /// <summary>The bot in a sentence a player reads, a notice: "the Hard bot", or "the bot".</summary>
    public string InSentence(string? spec) => Of(spec) is { } level ? $"the {level.Name} bot" : "the bot";

    /// <summary>What the operator is shown: the bot's own name, and its level when one seats it ("lookahead-55 · Hard").</summary>
    public string OperatorName(string spec, string name) => Of(spec) is { } level ? $"{name} · {level.Name}" : name;

    private sealed record LevelsDocument(IReadOnlyList<LevelEntry>? Levels);

    private sealed record LevelEntry(string? Level, string? Agent, IReadOnlyList<string>? Formerly);
}

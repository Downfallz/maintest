using System.Text.Json;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The rule set a table plays, read from a file.
/// </summary>
/// <remarks>
/// The tabletop rule set is not the simulator's: the board game is balanced for 8 to 16 rounds, and a playtest
/// run on the engine's thirty-round default is a playtest of a different game (<c>docs/tabletop/plan.md</c>).
/// Where that file should come from is an open question — the content hash does not cover the rule set, so a
/// session is only reproducible against a hash *and* a rule set (<c>docs/tabletop/components.md</c>, question
/// 6). Until it is answered the table takes a path and says out loud which rule set it is playing, rather than
/// defaulting where nobody would notice.
/// </remarks>
internal sealed record RuleSetFile
{
    /// <summary>camelCase, as every file this repository authors by hand is written.</summary>
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public int TeamSize { get; init; } = 3;

    public int EnergyPerRound { get; init; } = 2;

    public int EvolutionPicksPerRound { get; init; } = 2;

    public int RoundCap { get; init; } = 12;

    public double CriticalMultiplier { get; init; } = 2.0;

    public static RuleSet Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new ArgumentException($"Rule set '{path}' not found. It is a JSON object of teamSize, energyPerRound, evolutionPicksPerRound, roundCap and criticalMultiplier.", nameof(path));
        }

        var file = Parse(path);
        try
        {
            return RuleSet.Create(file.TeamSize, file.EnergyPerRound, file.EvolutionPicksPerRound, file.RoundCap, file.CriticalMultiplier);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ArgumentException($"Rule set '{path}' is not playable: {exception.Message}", nameof(path), exception);
        }
    }

    /// <summary>The line a table prints, and the one a print sheet carries: a rule set is an identity, not a default.</summary>
    public static string Describe(RuleSet rules, string? path)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var where = path is { Length: > 0 } ? path : "the engine default, no --rules given";
        return $"Rules {rules.TeamSize} creatures, {rules.EnergyPerRound} energy, {rules.EvolutionPicksPerRound} picks, {rules.RoundCap} rounds, x{rules.CriticalMultiplier} crit ({where})";
    }

    private static RuleSetFile Parse(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<RuleSetFile>(File.ReadAllText(path), Options)
                ?? throw new ArgumentException($"Rule set '{path}' is empty.", nameof(path));
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"Rule set '{path}' is not valid JSON: {exception.Message}", nameof(path), exception);
        }
    }
}

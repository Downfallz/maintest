using System.Globalization;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A signed-in player's own statistics (<c>GET /api/stats</c>), read off the results of their games, which their
/// partition holds alone (ADR 0168): how their games went, their streaks, and the creatures, mutations and spells
/// they played. Nobody reads another player's. Names come from this host's catalogue; a creature, mutation or spell
/// it no longer holds is shown by its id.
/// </summary>
internal sealed class StatsApi(IMatchResults results, AccountApi accounts, IGameResources resources)
{
    public const string Path = "/api/stats";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static bool Names(string path) => path == Path;

    public async Task<StudioResponse> HandleAsync(string method, AccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (method != "GET")
        {
            return StudioResponse.OfJson(new { error = "Stats.NoRoute", message = "A player's statistics are read, not written." }, Web, 405);
        }

        if (await accounts.WhoAsync(request) is not { } who)
        {
            return StudioResponse.OfJson(new { error = "Account.SignInRequired", message = "Sign in to see your statistics." }, Web, 401);
        }

        var stats = PlayerStats.Of(await results.ListForAsync(who.Account.Id), who.Account.Id);
        var shown = StudioResponse.OfJson(
            new
            {
                stats.Overall,
                stats.AgainstPlayers,
                stats.AgainstBots,
                formats = stats.Formats.Select(format => new { format.Format, format.Tally }),
                stats.Shared,
                streak = stats.Streak is { } streak ? new { outcome = OutcomeName(streak.Outcome), streak.Length } : null,
                stats.BestWinStreak,
                averageRounds = stats.AverageRounds is { } rounds ? Math.Round(rounds, 1) : (double?)null,
                stats.WithTeams,
                creatures = stats.Creatures.Select(creature => new { id = creature.Creature, name = CreatureName(creature.Creature), creature.Fielded, creature.Won, creature.Survived }),
                mutations = stats.Mutations.Select(mutation => new { id = mutation.Mutation, name = MutationName(mutation.Mutation), mutation.Bought, mutation.Won }),
                spells = stats.Spells.Select(spell => new { id = spell.Spell, name = SpellName(spell.Spell), spell.Casts, spell.Games, spell.Won }),
            },
            Web);

        // A player who only ever comes back to their statistics keeps their session, as one who opens the lobby does.
        return who.Renewed is { } renewed ? shown with { Headers = [new KeyValuePair<string, string>("Set-Cookie", renewed)] } : shown;
    }

    private static string OutcomeName(GameOutcome outcome) => outcome.ToString().ToLower(CultureInfo.InvariantCulture);

    private string CreatureName(string id) =>
        CreatureDefinitionId.TryParse(id, out var parsed) && resources.TryGetCreature(parsed, out var creature) ? creature.Name : id;

    private string MutationName(string id) =>
        TierId.TryParse(id, out var parsed) && resources.TryGetTier(parsed, out var tier) ? tier.Name : id;

    private string SpellName(string id) =>
        SpellId.TryParse(id, out var parsed) && resources.TryGetSpell(parsed, out var spell) ? spell.Name : id;
}

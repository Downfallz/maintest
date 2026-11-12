using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// A player's statistics, read off their ranked games (ADR 0171): games between two accounts, opened ranked, in
/// the ranked format, 3v3. How those games went, the streaks and how long the games last, and what the player
/// played in them, mutation by mutation and spell by spell. Games against a bot, unranked games and games on a shared
/// device are kept and listed elsewhere, and counted in nothing here. Only games recorded with their teams count
/// towards mutations and spells.
/// </summary>
public sealed record PlayerStats(
    Tally Ranked,
    StreakOf? Streak,
    int BestWinStreak,
    double? AverageRounds,
    int WithTeams,
    IReadOnlyList<MutationTally> Mutations,
    IReadOnlyList<SpellTally> Spells)
{
    /// <summary>The statistics of an account over its results; results that are not its ranked games are passed over.</summary>
    public static PlayerStats Of(IEnumerable<MatchResult> results, AccountId account)
    {
        ArgumentNullException.ThrowIfNull(results);
        var played = results
            .Where(result => Counts(result) && result.Seats.Count(seat => seat.Account == account) == 1)
            .OrderBy(result => result.FinishedAt).ThenBy(result => result.MatchId, StringComparer.Ordinal)
            .Select(result => (Result: result, Seat: result.Seats.Single(seat => seat.Account == account).Seat))
            .Select(game => (game.Result, game.Seat, Outcome: OutcomeFor(game.Result, game.Seat)))
            .ToArray();

        var teamed = played.Where(game => game.Result.Teams.Count > 0).ToArray();
        var fielded = teamed.SelectMany(game => game.Result.Teams.Where(creature => creature.Seat == game.Seat).Select(creature => (Creature: creature, game.Outcome))).ToArray();
        return new PlayerStats(
            Count(played.Select(game => game.Outcome)),
            CurrentStreak(played.Select(game => game.Outcome)),
            LongestWinRun(played.Select(game => game.Outcome)),
            played.Length == 0 ? null : played.Average(game => game.Result.Rounds),
            teamed.Length,
            [.. fielded.SelectMany(one => one.Creature.Mutations.Select(mutation => (Mutation: mutation, one.Outcome)))
                .GroupBy(bought => bought.Mutation, StringComparer.Ordinal)
                .Select(mutation => new MutationTally(mutation.Key, mutation.Count(), mutation.Count(bought => bought.Outcome == GameOutcome.Won)))
                .OrderByDescending(mutation => mutation.Bought).ThenBy(mutation => mutation.Mutation, StringComparer.Ordinal)],
            [.. teamed.SelectMany(game => game.Result.Teams.Where(creature => creature.Seat == game.Seat)
                    .SelectMany(creature => creature.Casts)
                    .GroupBy(cast => cast.Key, StringComparer.Ordinal)
                    .Select(spell => (Spell: spell.Key, Casts: spell.Sum(cast => cast.Value), game.Outcome)))
                .GroupBy(spell => spell.Spell, StringComparer.Ordinal)
                .Select(spell => new SpellTally(spell.Key, spell.Sum(game => game.Casts), spell.Count(), spell.Count(game => game.Outcome == GameOutcome.Won)))
                .OrderByDescending(spell => spell.Casts).ThenBy(spell => spell.Spell, StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Whether a result counts here (ADR 0171): ranked, as the ladder rates it, and 3v3, the one ranked format. A ranked
    /// game in another format, from before ranked games were 3v3, is not.
    /// </summary>
    public static bool Counts(MatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Ranked && MatchFormat.TryParse(result.Format, out var format) && format == MatchFormat.ThreeOnThree;
    }

    private static GameOutcome OutcomeFor(MatchResult result, string seat)
    {
        if (result.Winner is null)
        {
            return GameOutcome.Drawn;
        }

        return result.Winner == seat ? GameOutcome.Won : GameOutcome.Lost;
    }

    private static Tally Count(IEnumerable<GameOutcome> outcomes) => outcomes.Aggregate(Tally.None, (tally, outcome) => tally.With(outcome));

    /// <summary>The outcome the last games share and how many of them in a row; none before a first game.</summary>
    private static StreakOf? CurrentStreak(IEnumerable<GameOutcome> oldestFirst)
    {
        StreakOf? streak = null;
        foreach (var outcome in oldestFirst)
        {
            streak = streak is { } running && running.Outcome == outcome ? running with { Length = running.Length + 1 } : new StreakOf(outcome, 1);
        }

        return streak;
    }

    private static int LongestWinRun(IEnumerable<GameOutcome> oldestFirst)
    {
        int best = 0, running = 0;
        foreach (var outcome in oldestFirst)
        {
            running = outcome == GameOutcome.Won ? running + 1 : 0;
            best = Math.Max(best, running);
        }

        return best;
    }
}


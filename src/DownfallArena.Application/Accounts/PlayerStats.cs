using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// A player's statistics, read off the results of the games they held a seat in: how their games went, overall,
/// against players and bots and by format; their streaks and how long their games last; and what they played,
/// creature by creature, mutation by mutation and spell by spell. A game whose two seats one account held (a
/// shared device) is counted apart and in nothing else: it has no side that won. Only games recorded with their
/// teams count towards creatures, mutations and spells.
/// </summary>
public sealed record PlayerStats(
    Tally Overall,
    Tally AgainstPlayers,
    Tally AgainstBots,
    IReadOnlyList<FormatTally> Formats,
    int Shared,
    StreakOf? Streak,
    int BestWinStreak,
    double? AverageRounds,
    int WithTeams,
    IReadOnlyList<CreatureTally> Creatures,
    IReadOnlyList<MutationTally> Mutations,
    IReadOnlyList<SpellTally> Spells)
{
    /// <summary>The statistics of an account over its results; results it held no seat in are passed over.</summary>
    public static PlayerStats Of(IEnumerable<MatchResult> results, AccountId account)
    {
        ArgumentNullException.ThrowIfNull(results);
        var played = new List<(MatchResult Result, string Seat, GameOutcome Outcome)>();
        var shared = 0;
        foreach (var result in results.OrderBy(result => result.FinishedAt).ThenBy(result => result.MatchId, StringComparer.Ordinal))
        {
            var mine = result.Seats.Where(seat => seat.Account == account).Select(seat => seat.Seat).ToArray();
            if (mine.Length > 1)
            {
                shared++;
            }
            else if (mine.Length == 1)
            {
                played.Add((result, mine[0], OutcomeFor(result, mine[0])));
            }
        }

        var teamed = played.Where(game => game.Result.Teams.Count > 0).ToArray();
        var fielded = teamed.SelectMany(game => game.Result.Teams.Where(creature => creature.Seat == game.Seat).Select(creature => (Creature: creature, game.Outcome))).ToArray();
        return new PlayerStats(
            Count(played.Select(game => game.Outcome)),
            Count(played.Where(game => !game.Result.AgainstBot).Select(game => game.Outcome)),
            Count(played.Where(game => game.Result.AgainstBot).Select(game => game.Outcome)),
            [.. played.GroupBy(game => game.Result.Format, StringComparer.Ordinal)
                .Select(format => new FormatTally(format.Key, Count(format.Select(game => game.Outcome))))
                .OrderByDescending(format => format.Tally.Games).ThenBy(format => format.Format, StringComparer.Ordinal)],
            shared,
            CurrentStreak(played.Select(game => game.Outcome)),
            LongestWinRun(played.Select(game => game.Outcome)),
            played.Count == 0 ? null : played.Average(game => game.Result.Rounds),
            teamed.Length,
            [.. fielded.GroupBy(one => one.Creature.Creature, StringComparer.Ordinal)
                .Select(creature => new CreatureTally(creature.Key, creature.Count(), creature.Count(one => one.Outcome == GameOutcome.Won), creature.Count(one => one.Creature.Survived)))
                .OrderByDescending(creature => creature.Fielded).ThenBy(creature => creature.Creature, StringComparer.Ordinal)],
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


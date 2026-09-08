using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// Plays every seed twice with the agents swapped, which cancels the first-mover advantage of player 1, and
/// reports each agent over the seed pairs. Combat rates come from a <see cref="CombatStatsRecorder"/> when one
/// listens to the matches; without it they read zero.
/// </summary>
public sealed class EvaluationRunner(BatchRunner batches, CombatStatsRecorder? combat = null)
{
    public async Task<EvaluationResult> RunAsync(EvaluationScenario scenario, RunStamp stamp, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(stamp);
        ArgumentOutOfRangeException.ThrowIfZero(scenario.Seeds.Count);

        var aFirst = new IntentCounter();
        var bFirst = new IntentCounter();
        var first = await batches.RunAsync(Batch(scenario, scenario.AgentA, scenario.AgentB), aFirst, cancellationToken);
        var second = await batches.RunAsync(Batch(scenario, scenario.AgentB, scenario.AgentA), bFirst, cancellationToken);
        var pairs = scenario.Seeds.Select((seed, index) => new SeedPair(seed, first.Results[index], second.Results[index])).ToList();
        var all = first.Results.Concat(second.Results).ToList();

        return new EvaluationResult
        {
            Stamp = stamp,
            AgentA = Report(scenario.AgentA.ToString(), pairs, pair => pair.WinsOfA, pair => pair.ScoreOfA, pair => pair.RemainingHealthOfA, Usage(aFirst, PlayerSlot.Player1, bFirst, PlayerSlot.Player2), Combat(pairs, PlayerSlot.Player1, PlayerSlot.Player2)),
            AgentB = Report(scenario.AgentB.ToString(), pairs, pair => pair.WinsOfB, pair => 1 - pair.ScoreOfA, pair => pair.RemainingHealthOfB, Usage(bFirst, PlayerSlot.Player1, aFirst, PlayerSlot.Player2), Combat(pairs, PlayerSlot.Player2, PlayerSlot.Player1)),
            Matches = all.Count,
            Draws = all.Count(result => result.Outcome.IsDraw),
            AverageRounds = all.Average(result => result.Rounds),
            RoundCapShare = (double)all.Count(result => result.Outcome.Reason == MatchEndReason.RoundCap) / all.Count,
            Pairs = pairs,
        };
    }

    private static SimulationScenario Batch(EvaluationScenario scenario, AgentSpec player1, AgentSpec player2) => new()
    {
        RuleSet = scenario.RuleSet,
        Player1Roster = scenario.Roster,
        Player2Roster = scenario.Roster,
        Player1Agent = player1,
        Player2Agent = player2,
        Matches = scenario.Seeds.Count,
        Seeds = scenario.Seeds,
    };

    private static AgentReport Report(
        string agent,
        IReadOnlyList<SeedPair> pairs,
        Func<SeedPair, int> wins,
        Func<SeedPair, double> score,
        Func<SeedPair, int> health,
        IReadOnlyDictionary<string, int> usage,
        CombatStats combat)
    {
        var totalWins = pairs.Sum(wins);
        return new AgentReport
        {
            Agent = agent,
            Wins = totalWins,
            WinRate = PairedStatistics.Interval([.. pairs.Select(pair => wins(pair) / 2.0)]),
            Score = PairedStatistics.Interval([.. pairs.Select(score)]),
            AverageRemainingHealth = pairs.Average(pair => health(pair) / 2.0),
            SpellUsage = usage,
            SpellEntropy = PairedStatistics.Entropy(usage.Values),
            Actions = combat.Actions,
            Fizzles = combat.Fizzles,
            Criticals = combat.Criticals,
        };
    }

    private static IReadOnlyDictionary<string, int> Usage(IntentCounter firstBatch, PlayerSlot slotInFirst, IntentCounter secondBatch, PlayerSlot slotInSecond)
    {
        var usage = new Dictionary<string, int>(firstBatch.UsageOf(slotInFirst), StringComparer.Ordinal);
        foreach (var (spell, count) in secondBatch.UsageOf(slotInSecond))
        {
            usage[spell] = usage.GetValueOrDefault(spell) + count;
        }

        return usage;
    }

    private CombatStats Combat(IReadOnlyList<SeedPair> pairs, PlayerSlot slotWhenAFirst, PlayerSlot slotWhenBFirst)
    {
        var stats = new CombatStats(0, 0, 0);
        if (combat is null)
        {
            return stats;
        }

        foreach (var pair in pairs)
        {
            stats = stats.Plus(combat.Of(pair.AFirst.MatchId, slotWhenAFirst)).Plus(combat.Of(pair.BFirst.MatchId, slotWhenBFirst));
        }

        return stats;
    }
}

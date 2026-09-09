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

        // In self-play the second batch is a replay of the first, so its sides are the same sides again.
        // Counting them would double the sample the spell table's confidence rests on without adding a
        // single independent observation.
        var selfPlay = string.Equals(scenario.AgentA.ToString(), scenario.AgentB.ToString(), StringComparison.Ordinal);
        List<(IReadOnlyList<MatchResult> Results, IntentCounter Counter)> counted = selfPlay
            ? [(first.Results, aFirst)]
            : [(first.Results, aFirst), (second.Results, bFirst)];

        return new EvaluationResult
        {
            Stamp = stamp,
            AgentA = Report(scenario.AgentA.ToString(), pairs, pair => pair.WinsOfA, pair => pair.ScoreOfA, pair => pair.RemainingHealthOfA, Usage(aFirst, PlayerSlot.Player1, bFirst, PlayerSlot.Player2), Combat(pairs, PlayerSlot.Player1, PlayerSlot.Player2)),
            AgentB = Report(scenario.AgentB.ToString(), pairs, pair => pair.WinsOfB, pair => 1 - pair.ScoreOfA, pair => pair.RemainingHealthOfB, Usage(bFirst, PlayerSlot.Player1, aFirst, PlayerSlot.Player2), Combat(pairs, PlayerSlot.Player2, PlayerSlot.Player1)),
            Matches = all.Count,
            Draws = all.Count(result => result.Outcome.IsDraw),
            AverageRounds = all.Average(result => result.Rounds),
            RoundCapShare = (double)all.Count(result => result.Outcome.Reason == MatchEndReason.RoundCap) / all.Count,
            SelfPlay = selfPlay,
            SpellOutcomes = SpellOutcomes(counted, combat),
            Pairs = pairs,
        };
    }

    /// <summary>
    /// Every spell against the outcomes of the sides that declared it, and what its casts did. This is the
    /// content signal rather than the agent one: it says which spells the winning side was holding and leaning
    /// on, which is what tuning a number moves. Each batch brings its own results and the counter that watched
    /// them, so a batch that adds no independent sides is simply not passed in.
    /// </summary>
    private static List<SpellOutcome> SpellOutcomes(
        IReadOnlyList<(IReadOnlyList<MatchResult> Results, IntentCounter Counter)> counted,
        CombatStatsRecorder? combat)
    {
        var outcomes = counted
            .SelectMany(batch => batch.Results)
            .ToDictionary(result => result.MatchId, result => result.Outcome);
        var tally = new Dictionary<string, Tally>(StringComparer.Ordinal);

        foreach (var (match, slot, usage) in counted.SelectMany(batch => batch.Counter.Sides()))
        {
            if (!outcomes.TryGetValue(match, out var outcome))
            {
                continue;
            }

            var won = !outcome.IsDraw && outcome.Winner == slot;
            var effects = combat?.SpellsOf(match, slot);
            foreach (var (spell, intents) in usage)
            {
                var current = tally.GetValueOrDefault(spell) ?? new Tally();
                current.Intents += intents;
                current.Sides++;
                current.Wins += won ? 1 : 0;
                current.Losses += !outcome.IsDraw && outcome.Winner != slot ? 1 : 0;
                current.Draws += outcome.IsDraw ? 1 : 0;

                var cast = effects?.GetValueOrDefault(spell) ?? SpellEffects.None;
                current.Effects = current.Effects.Plus(cast);
                current.ResolvedWhenWon += won ? cast.Resolved : 0;
                tally[spell] = current;
            }
        }

        return
        [
            .. tally
                .Select(entry => new SpellOutcome
                {
                    Spell = entry.Key,
                    Intents = entry.Value.Intents,
                    Sides = entry.Value.Sides,
                    Wins = entry.Value.Wins,
                    Losses = entry.Value.Losses,
                    Draws = entry.Value.Draws,
                    Resolved = entry.Value.Effects.Resolved,
                    Fizzled = entry.Value.Effects.Fizzled,
                    Criticals = entry.Value.Effects.Criticals,
                    Damage = entry.Value.Effects.Damage,
                    Healing = entry.Value.Effects.Healing,
                    Energy = entry.Value.Effects.Energy,
                    Stuns = entry.Value.Effects.Stuns,
                    Bleeds = entry.Value.Effects.Bleeds,
                    Regens = entry.Value.Effects.Regens,
                    Buffs = entry.Value.Effects.Buffs,
                    ResolvedWhenWon = entry.Value.ResolvedWhenWon,
                })
                .OrderByDescending(outcome => outcome.Score)
                .ThenByDescending(outcome => outcome.Sides)
                .ThenBy(outcome => outcome.Spell, StringComparer.Ordinal),
        ];
    }

    /// <summary>One spell's running totals while the sides are walked.</summary>
    private sealed class Tally
    {
        public int Intents { get; set; }

        public int Sides { get; set; }

        public int Wins { get; set; }

        public int Losses { get; set; }

        public int Draws { get; set; }

        public int ResolvedWhenWon { get; set; }

        public SpellEffects Effects { get; set; } = SpellEffects.None;
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

    private static Dictionary<string, int> Usage(IntentCounter firstBatch, PlayerSlot slotInFirst, IntentCounter secondBatch, PlayerSlot slotInSecond)
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

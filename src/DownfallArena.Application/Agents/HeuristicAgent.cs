using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Agents;

/// <summary>
/// One-step lookahead with explicit weights: every decision picks the option whose expected outcome scores
/// best under <see cref="ActionScorer"/>. Deterministic: ties go to the first option in a stable order.
/// </summary>
public sealed class HeuristicAgent(ScoringWeights weights, IGameResources resources, RuleSet rules) : IPlayerAgent
{
    private readonly ActionScorer _scorer = new(resources, rules, weights);

    public ScoringWeights Weights => weights;

    /// <summary>Unlocks the spell worth the most on the current board, combat value plus the initiative it buys; passes only when nothing can be unlocked.</summary>
    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        var creatures = Creatures(board);
        EvolutionChoice? best = null;
        var bestScore = double.NegativeInfinity;
        foreach (var option in options.Creatures)
        {
            var actor = creatures.First(creature => creature.Id == option.Creature);
            foreach (var spell in option.UnlockableSpells.OrderBy(spell => spell.Value, StringComparer.Ordinal))
            {
                var score = _scorer.UnlockValue(actor, spell, creatures);
                if (score > bestScore)
                {
                    best = new EvolutionChoice(option.Creature, spell);
                    bestScore = score;
                }
            }
        }

        return best is null ? EvolutionDecision.Pass : EvolutionDecision.Unlock(best);
    }

    /// <summary>Quick when the creature can kill an enemy this round, Standard otherwise.</summary>
    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
    {
        ArgumentNullException.ThrowIfNull(board);

        var creatures = Creatures(board);
        var actor = creatures.First(candidate => candidate.Id == creature);
        var kills = Castable(actor).Any(spell => TargetSets.Of(TargetingRules.LegalTargets(actor, resources.GetSpell(spell), creatures))
            .Any(targets => _scorer.Kills(CombatAction.Bind(new CombatIntent(actor.Id, spell), targets), creatures)));
        return kills ? Speed.Quick : Speed.Standard;
    }

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(intentOption);

        var creatures = Creatures(board);
        var actor = creatures.First(creature => creature.Id == intentOption.Creature);
        SpellId? best = null;
        var bestScore = double.NegativeInfinity;
        foreach (var spell in intentOption.CastableSpells.OrderBy(spell => spell.Value, StringComparer.Ordinal))
        {
            var score = _scorer.Best(actor, spell, creatures)?.Score ?? -weights.Risk;
            if (score > bestScore)
            {
                best = spell;
                bestScore = score;
            }
        }

        return best ?? throw new InvalidOperationException("The options offer no castable spell.");
    }

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.LegalTargets.IsCastable)
        {
            return [];
        }

        var creatures = Creatures(board);
        var actor = creatures.First(creature => creature.Id == options.Actor);
        return _scorer.Best(actor, options.Spell, creatures)?.Targets ?? [];
    }

    private static List<CreatureSnapshot> Creatures(PlayerBoardState board) => [.. board.Allies, .. board.Enemies];

    private IEnumerable<SpellId> Castable(CreatureSnapshot actor) =>
        actor.KnownSpells.Where(spell => resources.GetSpell(spell).Stats.Cost.Value <= actor.Energy.Value).OrderBy(spell => spell.Value, StringComparer.Ordinal);
}

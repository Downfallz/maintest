using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Agents;

/// <summary>
/// One-step lookahead with explicit weights: every decision picks the option whose expected outcome scores
/// best under <see cref="ActionScorer"/>. Deterministic: ties go to the first option in a stable order.
/// </summary>
public sealed class HeuristicAgent(ScoringWeights weights, IGameResources resources, RuleSet rules) : IPlayerAgent
{
    private readonly ActionScorer _scorer = new(resources, rules, weights);
    private readonly Foresight _foresight = new(new ActionScorer(resources, rules, weights), resources, rules);

    public ScoringWeights Weights => weights;

    /// <summary>Buys the package worth the most on the current board, its best spell's combat value plus the initiative the package buys less the part of that spell's cost the actor cannot cover; passes only when nothing can be bought.</summary>
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
            foreach (var tier in option.AvailableTiers)
            {
                var score = _scorer.PurchaseValue(actor, tier, creatures);
                if (score > bestScore)
                {
                    best = new EvolutionChoice(option.Creature, tier);
                    bestScore = score;
                }
            }
        }

        return best is null ? EvolutionDecision.Pass : EvolutionDecision.Unlock(best);
    }

    /// <summary>
    /// Quick when the creature can kill an enemy this round without needing a critical, Standard otherwise.
    /// <para>
    /// The rule reads as a heuristic and is exact under the speed trade. <see cref="ActionScorer.Kills(CombatAction, IReadOnlyList{CreatureSnapshot})"/>
    /// forces the plain roll, so a kill it reports is one the actor lands whatever the dice say: the critical
    /// Quick gives up buys nothing that kill needs, and acting before the target does is free. When no plain
    /// kill exists the critical is the only thing left to hope for, and Standard is the only speed that can
    /// roll it.
    /// </para>
    /// <para>
    /// A one-step agent cannot do better here, and that is a property of the evaluation rather than of this
    /// rule: <see cref="ActionScorer"/> scores the board an action lands on and has no way to say "and it
    /// landed first", so Quick is strictly worse in everything it can measure. The value of going early is
    /// only visible to something that walks the timeline -- and a search cannot be asked either, because the
    /// timeline is built in <c>TurnOrderResolution</c>, after this decision. Speed is the one choice in the
    /// round no agent here can evaluate by playing it out.
    /// </para>
    /// </summary>
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
        var gone = _foresight.AlreadyDoomed(board, creatures, actor);
        SpellId? best = null;
        var bestScore = double.NegativeInfinity;
        foreach (var spell in intentOption.CastableSpells.OrderBy(spell => spell.Value, StringComparer.Ordinal))
        {
            var score = _scorer.Best(actor, spell, creatures, gone, SpeedOf(board, actor.Id))?.Score ?? 0;  // nothing to hit is worth nothing (ADR 0040)
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
        return _scorer.Best(actor, options.Spell, creatures, _foresight.GoneBeforeThisSlot(board, creatures), SpeedOf(board, actor.Id))?.Targets ?? [];
    }

    /// <summary>
    /// What a creature chose in the Speed sub-phase, which decides whether its cast can crit at all. Both
    /// decisions that use it -- the intent and its targets -- come after that sub-phase, so the choice is
    /// always there; Standard is the answer to a board asked out of order, not a creature that picked it.
    /// </summary>
    private static Speed SpeedOf(PlayerBoardState board, CreatureId creature) =>
        board.SpeedChoices.FirstOrDefault(choice => choice.Creature == creature)?.Speed ?? Speed.Standard;

    private static List<CreatureSnapshot> Creatures(PlayerBoardState board) => Foresight.Creatures(board);

    private IEnumerable<SpellId> Castable(CreatureSnapshot actor) =>
        actor.KnownSpells.Where(spell => resources.GetSpell(spell).Stats.Cost.Value <= actor.Energy.Value).OrderBy(spell => spell.Value, StringComparer.Ordinal);
}

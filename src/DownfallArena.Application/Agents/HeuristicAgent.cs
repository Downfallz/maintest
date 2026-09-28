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
    private readonly Foresight _foresight = new(new ActionScorer(resources, rules, weights));

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
    /// The order the roll-off left. The scorer reads one action at a time and has no view of which of two of
    /// its own creatures should act first, so it does not pretend to: a searching agent is where that belongs.
    /// </summary>
    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AsRolled;
    }

    /// <summary>
    /// Standard only when the critical it keeps is worth something: when the best expected score among the
    /// creature's castable spells is higher with its critical chance than without. Quick otherwise, and always
    /// when a castable spell kills an enemy without a critical (ADR 0084).
    /// <para>
    /// <see cref="ActionScorer"/> cannot see what acting first is worth -- it scores the board an action lands
    /// on, not when it lands -- so the rule does not weigh the two. It only asks whether Standard buys anything
    /// the scorer can see. A creature whose spells cannot crit, or whose crit changes nothing its weights
    /// price, gains nothing by waiting and goes first. Played against the rule it replaces, which went Standard
    /// whenever no plain kill was on the table, this one won every weight set measured, the hold-out seeds
    /// included (journal, 2026-09-28).
    /// </para>
    /// </summary>
    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
    {
        ArgumentNullException.ThrowIfNull(board);

        var creatures = Creatures(board);
        var actor = creatures.First(candidate => candidate.Id == creature);
        var castable = Castable(actor).ToList();
        var kills = castable.Any(spell => TargetSets.Of(TargetingRules.LegalTargets(actor, resources.GetSpell(spell), creatures))
            .Any(targets => _scorer.Kills(CombatAction.Bind(new CombatIntent(actor.Id, spell), targets), creatures)));
        if (kills)
        {
            return Speed.Quick;
        }

        return BestScore(actor, castable, creatures, Speed.Standard) > BestScore(actor, castable, creatures, Speed.Quick)
            ? Speed.Standard
            : Speed.Quick;
    }

    /// <summary>The best expected score among the spells at a speed; nothing to hit is worth nothing (ADR 0040).</summary>
    private double BestScore(CreatureSnapshot actor, IReadOnlyList<SpellId> spells, IReadOnlyList<CreatureSnapshot> creatures, Speed speed) =>
        spells.Select(spell => _scorer.Best(actor, spell, creatures, speed: speed)?.Score ?? 0).DefaultIfEmpty(0).Max();

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
        // The board already carries every action before this slot (ADR 0083): nobody is expected gone.
        return _scorer.Best(actor, options.Spell, creatures, ActionScorer.NoneGone, SpeedOf(board, actor.Id))?.Targets ?? [];
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

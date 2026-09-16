using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Agents;

/// <summary>
/// Decides a combat move by playing the round out (ADR 0047). Where the heuristic agent prices a move by what
/// it does on the board it is cast on, this one puts the move on the board, plays every later activation slot
/// the way the heuristic agent would play it, and sums what the scorer says of every action the round then
/// holds, allies for and enemies against. So it sees what the one-step reading cannot: a stun that takes away
/// the enemy's kill, a kill that lands before the target acts, a target that another action will have killed
/// first, a defense buff that turns a lethal round into a survivable one, all through the same rules the
/// match runs, none of it guessed. The sum is the scorer's own calibration: a move with no consequence for
/// the rest of the round is worth exactly what the greedy agent says it is, and only the interactions are
/// new. A board valued on its own instead, health and conditions priced by the same weights, measured eight
/// points of win rate worse against the greedy agent, whose weights were tuned for the one-step reading and
/// not for a reading of the board it leaves (journal, 2026-09-16).
/// <para>
/// What it does not see is hidden: an enemy's intent is not public until revealed, so an enemy slot that is
/// still ahead plays what the scorer would play for it, and an ally that has not declared plays the same way.
/// What has been declared or revealed is read as it is. The round is played on the plain roll, which is what
/// makes the agent deterministic and the digest replayable; a critical that would change the answer is a
/// chance, not a reading.
/// </para>
/// <para>
/// Evolution and speed are the heuristic agent's: neither is a combat move, and the round they plan has no
/// timeline yet to play out.
/// </para>
/// </summary>
public sealed class LookaheadAgent(ScoringWeights weights, IGameResources resources, RuleSet rules) : IPlayerAgent
{
    private readonly HeuristicAgent _oneStep = new(weights, resources, rules);
    private readonly ActionScorer _scorer = new(resources, rules, weights);

    public ScoringWeights Weights => weights;

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => _oneStep.DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => _oneStep.DecideSpeed(board, creature);

    /// <summary>
    /// The spell whose round ends best: for each castable spell, the round played out from its first slot with
    /// the actor casting that spell on the best targets the board offers when its slot comes. Ties go to the
    /// first spell in ordinal id order.
    /// </summary>
    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(intentOption);

        var creatures = Creatures(board);
        var actor = creatures.First(creature => creature.Id == intentOption.Creature);
        var declared = DeclaredSpells(board, creatures, intentOption.Creature);
        SpellId? best = null;
        var bestValue = (Round: double.NegativeInfinity, OneStep: double.NegativeInfinity);
        foreach (var spell in intentOption.CastableSpells.OrderBy(spell => spell.Value, StringComparer.Ordinal))
        {
            var value = (
                Round: PlayOut(board, creatures, 0, intentOption.Creature, declared, ahead => BestTargets(ahead, intentOption.Creature, spell)),
                OneStep: _scorer.Best(actor, spell, creatures)?.Score ?? 0);
            if (Better(value, bestValue))
            {
                best = spell;
                bestValue = value;
            }
        }

        return best ?? throw new InvalidOperationException("The options offer no castable spell.");
    }

    /// <summary>
    /// The target set whose round ends best, on the board the actions already revealed leave: they are bound
    /// in timeline order and public, so the board this actor's action lands on is a reading, not a guess
    /// (ADR 0039, made exact by ADR 0047). Ties go to the first set in candidate order; no target when the
    /// spell is no longer castable.
    /// </summary>
    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.LegalTargets.IsCastable)
        {
            return [];
        }

        var beforeCombat = Creatures(board);
        var declared = DeclaredSpells(board, beforeCombat, options.Actor);
        IReadOnlyList<CreatureSnapshot> ahead = beforeCombat;
        foreach (var revealed in board.RevealedActions)
        {
            ahead = Advance.Action(revealed, ahead, resources, rules, ForcedRandom.NotCritical).Board;
        }

        var intent = new CombatIntent(options.Actor, options.Spell);
        IReadOnlyList<CreatureId> best = [];
        var bestValue = (Round: double.NegativeInfinity, OneStep: double.NegativeInfinity);
        foreach (var targets in TargetSets.Of(options.LegalTargets))
        {
            var action = CombatAction.Bind(intent, targets);
            var value = (
                Round: PlayOut(board, ahead, board.RevealedActions.Count, options.Actor, declared, _ => action),
                OneStep: _scorer.Expected(action, ahead));
            if (Better(value, bestValue))
            {
                best = targets;
                bestValue = value;
            }
        }

        return best;
    }

    /// <summary>
    /// The round played out first, the one-step score second. The second key is not a refinement: it is
    /// what decides when the first cannot. The rollout stands in for the hidden slots with a guess, and when
    /// that guess has the actor dead or stunned before its own slot, every candidate leaves the same board and
    /// the round says nothing about the choice. The choice still matters in every world where the guess is
    /// wrong and the actor does act, and the one-step reading is the best that can be said about those.
    /// Without it the tie went to the first spell in id order, which is how the weakest spell in the
    /// catalogue came to be cast three times more often than the greedy agent casts it.
    /// </summary>
    private static bool Better((double Round, double OneStep) candidate, (double Round, double OneStep) best) =>
        candidate.Round > best.Round || (candidate.Round == best.Round && candidate.OneStep > best.OneStep);

    /// <summary>
    /// What the round is worth when played out from a slot: the actor plays what the candidate says at its
    /// slot, every other creature plays what can be read of it, and every action is scored on the board it
    /// lands on, the actor's team's for and the other's against, with a match the round ends worth the whole
    /// board one way or the other.
    /// </summary>
    private double PlayOut(
        PlayerBoardState board,
        IReadOnlyList<CreatureSnapshot> start,
        int fromSlot,
        CreatureId actor,
        Dictionary<CreatureId, SpellId?> declared,
        Func<IReadOnlyList<CreatureSnapshot>, CombatAction?> candidate)
    {
        var chance = CriticalChance(start, actor, candidate);
        var plain = PlayOut(board, start, fromSlot, actor, declared, candidate, ForcedRandom.NotCritical);
        return chance == 0 ? plain : (chance * PlayOut(board, start, fromSlot, actor, declared, candidate, ForcedRandom.Critical)) + ((1 - chance) * plain);
    }

    /// <summary>
    /// The actor's chance of a critical on the candidate, read the way <see cref="ActionScorer.Expected"/>
    /// reads it, so the rollout is weighted between its two rolls rather than taken on a miss: taken on a
    /// miss, a spell that crits three casts in four is priced at half of what it does, and the agent stops
    /// casting it. Only the actor's own roll is weighted; every other creature's stays a miss, which keeps
    /// the cost at two rounds per candidate rather than two to the power of the slots left.
    /// </summary>
    private double CriticalChance(IReadOnlyList<CreatureSnapshot> start, CreatureId actor, Func<IReadOnlyList<CreatureSnapshot>, CombatAction?> candidate)
    {
        var action = candidate(start);
        if (action is null)
        {
            return 0;
        }

        var snapshot = start.First(creature => creature.Id == actor);
        return snapshot.CriticalChance.Plus(resources.GetSpell(action.Spell).Stats.CriticalChance.Value).Value;
    }

    private double PlayOut(
        PlayerBoardState board,
        IReadOnlyList<CreatureSnapshot> start,
        int fromSlot,
        CreatureId actor,
        Dictionary<CreatureId, SpellId?> declared,
        Func<IReadOnlyList<CreatureSnapshot>, CombatAction?> candidate,
        ForcedRandom roll)
    {
        var ahead = start;
        var value = 0.0;
        for (var index = fromSlot; index < board.Timeline.Count; index++)
        {
            var creature = board.Timeline[index].Creature;
            var action = creature == actor ? candidate(ahead) : Guess(ahead, creature, declared[creature]);
            if (action is null)
            {
                continue;
            }

            var advanced = Advance.Action(action, ahead, resources, rules, creature == actor ? roll : ForcedRandom.NotCritical);
            var sign = ahead.First(candidate => candidate.Id == creature).Owner == board.Slot ? 1 : -1;
            value += sign * _scorer.Score(advanced.Resolution, ahead);
            ahead = advanced.Board;
        }

        var cleaned = Advance.Cleanup(ahead, resources);
        return Advance.Outcome(cleaned, resources, board.RoundNumber ?? 1, rules) switch
        {
            null => value,
            { Winner: null } => value,
            { Winner: var winner } => value + (winner == board.Slot ? WholeBoard(cleaned) : -WholeBoard(cleaned)),
        };
    }

    /// <summary>
    /// What a match won on the spot is worth: every creature on the board at full health, the kill included.
    /// Large enough that no round's play outweighs it, and in the unit of everything else.
    /// </summary>
    private double WholeBoard(IReadOnlyList<CreatureSnapshot> board) =>
        board.Sum(creature => weights.Kill + (weights.Damage * creature.MaxHealth.Value));

    /// <summary>
    /// What a creature other than the actor plays at its slot: its spell on the best targets the board
    /// offers at that moment, or nothing when it is dead, stunned, or has no legal target, as its slot would
    /// fizzle or bind no target in the match too.
    /// <para>
    /// The spell is what the creature has declared when it is an ally that has, and otherwise the spell the
    /// scorer would declare for it <em>on the board before combat</em>, never on the board at its slot. That
    /// is the information the match gives: every intent is declared before anything resolves, so a creature
    /// does not get to pick its spell after seeing what the actor did. Guessing it at the slot instead makes
    /// every enemy clairvoyant, and a clairvoyant enemy punishes every aggressive move the actor could make,
    /// which read as the lookahead losing to the greedy agent three matches in four. Targets are the one
    /// thing a creature does choose after the earlier slots have revealed, so those are read at the slot.
    /// </para>
    /// </summary>
    private CombatAction? Guess(IReadOnlyList<CreatureSnapshot> ahead, CreatureId creature, SpellId? spell)
    {
        var snapshot = ahead.First(candidate => candidate.Id == creature);
        return spell is null || snapshot.IsDead || snapshot.IsStunned ? null : BestTargets(ahead, creature, spell);
    }

    /// <summary>
    /// The spell every creature but the actor is taken to have declared: the declared one for an ally that
    /// has, the scorer's pick on the board before combat for everyone else, or nothing when it has no castable
    /// spell with a legal target.
    /// </summary>
    private Dictionary<CreatureId, SpellId?> DeclaredSpells(PlayerBoardState board, IReadOnlyList<CreatureSnapshot> beforeCombat, CreatureId actor)
    {
        var spells = new Dictionary<CreatureId, SpellId?>();
        foreach (var slot in board.Timeline)
        {
            if (slot.Creature == actor || spells.ContainsKey(slot.Creature))
            {
                continue;
            }

            var snapshot = beforeCombat.First(candidate => candidate.Id == slot.Creature);
            spells[slot.Creature] = snapshot.Owner == board.Slot && board.Intents.FirstOrDefault(intent => intent.Actor == slot.Creature) is { } declared
                ? declared.Spell
                : GreedyIntent(snapshot, beforeCombat);
        }

        return spells;
    }

    /// <summary>The spell the scorer would declare for a creature on a board, as the greedy agent declares it.</summary>
    private SpellId? GreedyIntent(CreatureSnapshot snapshot, IReadOnlyList<CreatureSnapshot> creatures)
    {
        SpellId? best = null;
        var bestScore = double.NegativeInfinity;
        foreach (var spell in snapshot.KnownSpells.OrderBy(spell => spell.Value, StringComparer.Ordinal))
        {
            if (resources.GetSpell(spell).Stats.Cost.Value > snapshot.Energy.Value)
            {
                continue;
            }

            var score = _scorer.Best(snapshot, spell, creatures)?.Score;
            if (score is { } found && found > bestScore)
            {
                best = spell;
                bestScore = found;
            }
        }

        return best;
    }

    /// <summary>The spell on its best targets by the one-step reading, or nothing when it has no legal target.</summary>
    private CombatAction? BestTargets(IReadOnlyList<CreatureSnapshot> ahead, CreatureId actor, SpellId spell)
    {
        var snapshot = ahead.First(candidate => candidate.Id == actor);
        return _scorer.Best(snapshot, spell, ahead) is { } found
            ? CombatAction.Bind(new CombatIntent(actor, spell), found.Targets)
            : null;
    }

    private static List<CreatureSnapshot> Creatures(PlayerBoardState board) => [.. board.Allies, .. board.Enemies];
}

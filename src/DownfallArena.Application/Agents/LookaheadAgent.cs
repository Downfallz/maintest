using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

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
/// Adversarial, it is the minimax agent: an enemy slot that is still ahead is not guessed but played as the
/// reply that costs the actor most among the spells that enemy can cast, one enemy at a time in timeline
/// order, the others held at what was already taken for them. That reads the round against an opponent who
/// sees the actor's move, which no opponent in this game does, since intents are simultaneous: it is the
/// floor of what the move is worth, not its expectation, and whether a floor plays better than a guess is
/// what the journal measures.
/// </para>
/// <para>
/// Evolution and speed are the heuristic agent's: neither is a combat move, and the round they plan has no
/// timeline yet to play out. The tie order is this agent's own: it comes once the timeline is built, so the
/// round each order leads to can be played out like any other move.
/// </para>
/// </summary>
public sealed class LookaheadAgent(ScoringWeights weights, IGameResources resources, RuleSet rules, bool adversarial = false, IPlayerAgent? inner = null) : IPlayerAgent
{
    /// <summary>
    /// The agent that plays every seat this one has to guess: an ally that has not declared as the round is
    /// played out, and the evolution and the speed, which are not combat moves. The heuristic agent on the
    /// same weights unless a spec names another, which is what lets the search be built on a policy and so
    /// lets the loop improve its own last output (ADR 0055). The evaluation is deliberately not its: a
    /// clone's scores are logits and a value policy's are returns under its own baseline, and neither can be
    /// summed over a round.
    /// </summary>
    private readonly IPlayerAgent _oneStep = inner ?? new HeuristicAgent(weights, resources, rules);

    private readonly ActionScorer _scorer = new(resources, rules, weights);

    public ScoringWeights Weights => weights;

    /// <summary>Whether enemy slots are played as the worst reply rather than as the guessed one: the minimax agent.</summary>
    public bool IsAdversarial => adversarial;

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => _oneStep.DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => _oneStep.DecideSpeed(board, creature);

    /// <summary>
    /// The seating of its own tied creatures whose round ends best (ADR 0063): each way of putting them in
    /// the places the roll-off gave this side, the round played out from the first slot on it. No intent is
    /// declared yet, on either side, so every creature plays what this agent would guess for it, guessed once
    /// on the board as rolled: an ally what the agent it is built on would declare, each on its own rather than
    /// reading the others, and an enemy what the scorer would, the enemy's own tie kept as rolled. The minimax
    /// agent reads it the same way, since a worst reply is only defined against one actor's move and here every
    /// ally moves. Ties go to the order as rolled.
    /// <para>
    /// The seatings are the product of each tie's permutations, team size factorial at most: six on the
    /// default rules. Past <see cref="MostSeatings"/> the roll is kept rather than let a large team make one
    /// decision cost thousands of rounds.
    /// </para>
    /// </summary>
    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        var best = options.AsRolled;
        if (Seatings(options.Ties) > MostSeatings)
        {
            return best;
        }

        var creatures = Creatures(board);
        var guesses = Guesses(board, creatures);
        var bestValue = RoundValue.Lowest;
        foreach (var order in Orders(options.Ties))
        {
            var seated = board with { Timeline = Seat(board.Timeline, order) };
            var value = PlayOut(seated, creatures, 0, null, new Dictionary<CreatureId, SpellId?>(guesses), _ => null, ForcedRandom.NotCritical);
            if (value.CompareTo(bestValue) > 0)
            {
                best = order;
                bestValue = value;
            }
        }

        return best;
    }

    /// <summary>The most seatings a tie order plays out: a team of four, all tied.</summary>
    private const int MostSeatings = 24;

    /// <summary>
    /// How many seatings the ties have, counted only as far as <see cref="MostSeatings"/>: past it the count
    /// stops at one more, so a tie of twenty-one creatures cannot overflow the product back under the limit.
    /// </summary>
    private static int Seatings(IReadOnlyList<IReadOnlyList<CreatureId>> ties)
    {
        var seatings = 1;
        foreach (var tie in ties)
        {
            for (var factor = 2; factor <= tie.Count; factor++)
            {
                seatings *= factor;
                if (seatings > MostSeatings)
                {
                    return MostSeatings + 1;
                }
            }
        }

        return seatings;
    }

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
        var candidates = intentOption.CastableSpells
            .OrderBy(spell => spell.Value, StringComparer.Ordinal)
            .Select(spell => (
                Spell: spell,
                Round: Value(board, creatures, creatures, 0, intentOption.Creature, DeclaredSpells(board, creatures, intentOption.Creature, spell), ahead => BestTargets(board, ahead, intentOption.Creature, spell)),
                OneStep: _scorer.Best(actor, spell, creatures, speed: SpeedOf(board, actor.Id))?.Score ?? 0))
            .ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("The options offer no castable spell.");
        }

        // The rollout guesses every slot before the actor's. When that guess leaves the actor unable to cast any
        // spell that costs energy at its own slot -- dead, stunned, or drained below the cost -- the round is
        // ranking the guess and not the spell: only a free spell still resolves in it, and it wins by what it
        // gives, in the one world where the guess is right. The choice matters in every other world, and the
        // one-step reading is what can be said of those, as it is when the actor is guessed dead (journal,
        // 2026-09-23: the lookahead cast Wait 248 times against greedy for exactly this). Minimax keeps its
        // round: its reply is not a guess but the worst the enemy can do, and against that the free spell is the
        // one that still resolves.
        var paid = candidates.Where(candidate => resources.GetSpell(candidate.Spell).Stats.Cost.Value > 0).ToList();
        if (!adversarial && paid.Count > 0 && paid.All(candidate => candidate.Round.ActorStopped))
        {
            return candidates.MaxBy(candidate => candidate.OneStep).Spell;
        }

        return candidates.Skip(1)
            .Aggregate(candidates[0], (best, candidate) => Better((candidate.Round, candidate.OneStep), (best.Round, best.OneStep)) ? candidate : best)
            .Spell;
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
        var declared = DeclaredSpells(board, beforeCombat, options.Actor, options.Spell);
        IReadOnlyList<CreatureSnapshot> ahead = beforeCombat;
        foreach (var revealed in board.RevealedActions)
        {
            // Already revealed and replayed plain: the roll is forced, so the speed changes nothing here.
            ahead = Advance.Action(revealed, ahead, resources, rules, ForcedRandom.NotCritical, Speed.Standard).Board;
        }

        var intent = new CombatIntent(options.Actor, options.Spell);
        IReadOnlyList<CreatureId> best = [];
        var bestValue = (Round: RoundValue.Lowest, OneStep: double.NegativeInfinity);
        foreach (var targets in TargetSets.Of(options.LegalTargets))
        {
            var action = CombatAction.Bind(intent, targets);
            var value = (
                Round: Value(board, ahead, beforeCombat, board.RevealedActions.Count, options.Actor, new Dictionary<CreatureId, SpellId?>(declared), _ => action),
                OneStep: _scorer.Expected(action, ahead, speed: SpeedOf(board, options.Actor)));
            if (Better(value, bestValue))
            {
                best = targets;
                bestValue = value;
            }
        }

        return best;
    }

    /// <summary>
    /// What this agent takes every other creature on the timeline to play when the actor declares a candidate:
    /// an ally's declared spell or the one the heuristic agent would declare for it, an enemy's guessed spell,
    /// or, for the minimax agent, the reply that costs the actor most. A reading a test or a viewer can ask
    /// for; the decisions above are made on it.
    /// </summary>
    public IReadOnlyDictionary<CreatureId, SpellId?> Replies(PlayerBoardState board, CreatureId actor, SpellId candidate)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(candidate);

        var creatures = Creatures(board);
        var declared = DeclaredSpells(board, creatures, actor, candidate);
        Value(board, creatures, creatures, 0, actor, declared, ahead => BestTargets(board, ahead, actor, candidate));
        return declared;
    }

    /// <summary>
    /// The round's worth for a candidate: played out on the replies as they stand, or, adversarially, with
    /// each enemy slot still ahead settled at the reply that costs the actor most. One enemy at a time in
    /// timeline order, each over the spells it can cast, the earlier ones already settled and the later ones
    /// still at their guess: a joint worst case over every enemy would cost the product of their spell counts
    /// where this costs the sum, and the round is short enough that the difference between the two is rarely
    /// a different reply. The map is left holding what was settled, which is what <see cref="Replies"/> reads.
    /// The replies an enemy can choose from are read on the board before combat, where it declared: the
    /// round is played from <paramref name="start"/>, which at reveal time has the revealed actions on it,
    /// and an energy one of those gave or drained changes what the enemy can cast now, not what it could
    /// declare then.
    /// </summary>
    private RoundValue Value(
        PlayerBoardState board,
        IReadOnlyList<CreatureSnapshot> start,
        IReadOnlyList<CreatureSnapshot> beforeCombat,
        int fromSlot,
        CreatureId actor,
        Dictionary<CreatureId, SpellId?> declared,
        Func<IReadOnlyList<CreatureSnapshot>, CombatAction?> candidate)
    {
        var value = PlayOut(board, start, fromSlot, actor, declared, candidate);
        if (!adversarial)
        {
            return value;
        }

        foreach (var enemy in board.Timeline.Skip(fromSlot).Select(slot => slot.Creature).Distinct())
        {
            var snapshot = beforeCombat.First(creature => creature.Id == enemy);
            if (enemy == actor || snapshot.Owner == board.Slot)
            {
                continue;
            }

            var worst = declared[enemy];
            foreach (var spell in Castable(snapshot))
            {
                declared[enemy] = spell;
                var replied = PlayOut(board, start, fromSlot, actor, declared, candidate);
                if (replied.CompareTo(value) < 0)
                {
                    value = replied;
                    worst = spell;
                }
            }

            declared[enemy] = worst;
        }

        return value;
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
    private static bool Better((RoundValue Round, double OneStep) candidate, (RoundValue Round, double OneStep) best)
    {
        var round = candidate.Round.CompareTo(best.Round);
        return round > 0 || (round == 0 && candidate.OneStep > best.OneStep);
    }

    /// <summary>
    /// What the round is worth when played out from a slot: the actor plays what the candidate says at its
    /// slot, every other creature plays what can be read of it, and every action is scored on the board it
    /// lands on, the actor's team's for and the other's against, under the match the round ends, if it ends
    /// one, which outranks the score.
    /// </summary>
    private RoundValue PlayOut(
        PlayerBoardState board,
        IReadOnlyList<CreatureSnapshot> start,
        int fromSlot,
        CreatureId actor,
        Dictionary<CreatureId, SpellId?> declared,
        Func<IReadOnlyList<CreatureSnapshot>, CombatAction?> candidate)
    {
        var chance = CriticalChance(board, start, actor, candidate);
        var plain = PlayOut(board, start, fromSlot, actor, declared, candidate, ForcedRandom.NotCritical);
        return chance == 0 ? plain : RoundValue.Mix(chance, PlayOut(board, start, fromSlot, actor, declared, candidate, ForcedRandom.Critical), plain);
    }

    /// <summary>
    /// The round from a slot with the actor on its candidate and its roll, or, with no actor, every creature on
    /// what can be read or guessed of it and every roll plain.
    /// </summary>
    private RoundValue PlayOut(
        PlayerBoardState board,
        IReadOnlyList<CreatureSnapshot> start,
        int fromSlot,
        CreatureId? actor,
        Dictionary<CreatureId, SpellId?> declared,
        Func<IReadOnlyList<CreatureSnapshot>, CombatAction?> candidate,
        ForcedRandom roll)
    {
        var ahead = start;
        var value = 0.0;
        var stopped = false;
        for (var index = fromSlot; index < board.Timeline.Count; index++)
        {
            var creature = board.Timeline[index].Creature;
            var action = creature == actor ? candidate(ahead) : Guess(board, ahead, creature, declared[creature]);
            if (action is null)
            {
                stopped |= creature == actor && ahead.First(snapshot => snapshot.Id == creature) is { IsDead: true } or { IsStunned: true };
                continue;
            }

            // The timeline knows what each creature chose, and the actor's roll is the one that can crit, so the
            // rollout has to read the real speed here or it prices a critical a Quick actor cannot roll.
            var advanced = Advance.Action(action, ahead, resources, rules, creature == actor ? roll : ForcedRandom.NotCritical, board.Timeline[index].Speed);
            stopped |= creature == actor && Stops(advanced.Resolution.FizzleReason);
            var sign = ahead.First(candidate => candidate.Id == creature).Owner == board.Slot ? 1 : -1;
            value += sign * _scorer.Score(advanced.Resolution, ahead);
            ahead = advanced.Board;
        }

        var cleaned = Advance.Cleanup(ahead, resources);
        return Advance.Outcome(cleaned, resources, board.RoundNumber ?? 1, rules) switch
        {
            null => new RoundValue(0, value, stopped),
            { Winner: null } => new RoundValue(0, value, stopped),
            { Winner: var winner } => new RoundValue(winner == board.Slot ? 1 : -1, value, stopped),
        };
    }

    /// <summary>Whether a fizzle says the actor could not act at all, rather than that its spell missed.</summary>
    private static bool Stops(DomainError? fizzle) =>
        fizzle == CombatErrors.ActorDead || fizzle == CombatErrors.ActorStunned || fizzle == CombatErrors.NotEnoughEnergy;

    /// <summary>
    /// The actor's chance of a critical on the candidate, read the way <see cref="ActionScorer.Expected"/>
    /// reads it, so the rollout is weighted between its two rolls rather than taken on a miss: taken on a
    /// miss, a spell that crits three casts in four is priced at half of what it does, and the agent stops
    /// casting it. Only the actor's own roll is weighted; every other creature's stays a miss, which keeps
    /// the cost at two rounds per candidate rather than two to the power of the slots left.
    /// </summary>
    private double CriticalChance(PlayerBoardState board, IReadOnlyList<CreatureSnapshot> start, CreatureId actor, Func<IReadOnlyList<CreatureSnapshot>, CombatAction?> candidate)
    {
        var action = candidate(start);
        if (action is null)
        {
            return 0;
        }

        return ResolutionRules.CriticalChanceOf(start.First(creature => creature.Id == actor), resources.GetSpell(action.Spell), SpeedOf(board, actor));
    }

    /// <summary>
    /// What a round played out is worth: the match it ends first, then the score of its actions. A win is
    /// worth more than any score and a loss less than any, whatever a weights file says, because the two are
    /// never added: a weights file only has to be finite, so no bound in its unit could be trusted to
    /// dominate it, and a win an agent could be paid to decline is not a win it reads. Between two rolls,
    /// the outcome mixes the way the score does, so a critical that wins on the spot three casts in four is
    /// worth three quarters of a win.
    /// <para>
    /// <c>ActorStopped</c> is not part of the order: it says the rollout had the actor unable to act at its own
    /// slot, which <see cref="DecideIntent"/> reads before it trusts the order at all.
    /// </para>
    /// </summary>
    private readonly record struct RoundValue(double Outcome, double Score, bool ActorStopped = false) : IComparable<RoundValue>
    {
        public static RoundValue Lowest => new(double.NegativeInfinity, double.NegativeInfinity);

        public static RoundValue Mix(double chance, RoundValue critical, RoundValue plain) =>
            new((chance * critical.Outcome) + ((1 - chance) * plain.Outcome), (chance * critical.Score) + ((1 - chance) * plain.Score), plain.ActorStopped);

        public int CompareTo(RoundValue other)
        {
            var outcome = Outcome.CompareTo(other.Outcome);
            return outcome != 0 ? outcome : Score.CompareTo(other.Score);
        }
    }

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
    private CombatAction? Guess(PlayerBoardState board, IReadOnlyList<CreatureSnapshot> ahead, CreatureId creature, SpellId? spell)
    {
        var snapshot = ahead.First(candidate => candidate.Id == creature);
        return spell is null || snapshot.IsDead || snapshot.IsStunned ? null : BestTargets(board, ahead, creature, spell);
    }

    /// <summary>
    /// The spell every creature but the actor is taken to have declared, on the board before combat. An ally
    /// that has declared: its declared spell. An ally that has not: what the heuristic agent would declare
    /// for it once the actor's candidate is on the board too, since that ally will decide after this one and
    /// reads the team's declarations (ADR 0039), so a target the candidate kills is one it will not aim at.
    /// An enemy: the scorer's pick on the board before combat, its own declarations being hidden. Nothing
    /// for a creature with no castable spell.
    /// </summary>
    private Dictionary<CreatureId, SpellId?> DeclaredSpells(PlayerBoardState board, IReadOnlyList<CreatureSnapshot> beforeCombat, CreatureId actor, SpellId candidate)
    {
        var withCandidate = board.Intents.Any(intent => intent.Actor == actor)
            ? board
            : board with { Intents = [.. board.Intents, new CombatIntent(actor, candidate)] };
        var spells = new Dictionary<CreatureId, SpellId?>();
        foreach (var creature in board.Timeline.Select(slot => slot.Creature))
        {
            if (creature == actor || spells.ContainsKey(creature))
            {
                continue;
            }

            var snapshot = beforeCombat.First(other => other.Id == creature);
            spells[creature] = snapshot.Owner != board.Slot
                ? GreedyIntent(board, snapshot, beforeCombat)
                : board.Intents.FirstOrDefault(intent => intent.Actor == creature)?.Spell ?? AllyIntent(withCandidate, snapshot);
        }

        return spells;
    }

    /// <summary>
    /// Every tie's orders, one tie after the other, the order as rolled first: the product of each tie's
    /// permutations, flattened in timeline order the way a submitted tie order is.
    /// </summary>
    private static IEnumerable<IReadOnlyList<CreatureId>> Orders(IReadOnlyList<IReadOnlyList<CreatureId>> ties)
    {
        IEnumerable<IReadOnlyList<CreatureId>> orders = [[]];
        foreach (var tie in ties)
        {
            var captured = orders;
            orders = captured.SelectMany(before => Permutations(tie).Select(after => (IReadOnlyList<CreatureId>)[.. before, .. after]));
        }

        return orders;
    }

    /// <summary>A list's permutations, the list itself first.</summary>
    private static IEnumerable<IReadOnlyList<CreatureId>> Permutations(IReadOnlyList<CreatureId> tie)
    {
        if (tie.Count <= 1)
        {
            yield return tie;
            yield break;
        }

        for (var first = 0; first < tie.Count; first++)
        {
            var rest = tie.Where((_, index) => index != first).ToList();
            foreach (var tail in Permutations(rest))
            {
                yield return [tie[first], .. tail];
            }
        }
    }

    /// <summary>
    /// The timeline with an order's creatures in the places they hold, in that order. The places are theirs,
    /// in timeline order, and the slots move whole: two slots in one tie differ only in whose they are.
    /// </summary>
    private static List<ActivationSlot> Seat(IReadOnlyList<ActivationSlot> timeline, IReadOnlyList<CreatureId> order)
    {
        var slots = timeline.Where(slot => order.Contains(slot.Creature)).ToDictionary(slot => slot.Creature);
        var seated = timeline.ToList();
        var next = 0;
        for (var index = 0; index < seated.Count; index++)
        {
            if (slots.ContainsKey(seated[index].Creature))
            {
                seated[index] = slots[order[next++]];
            }
        }

        return seated;
    }

    /// <summary>What every creature on the timeline is taken to declare before anyone has: an ally what the agent this one is built on would, an enemy what the scorer would.</summary>
    private Dictionary<CreatureId, SpellId?> Guesses(PlayerBoardState board, IReadOnlyList<CreatureSnapshot> creatures)
    {
        var spells = new Dictionary<CreatureId, SpellId?>();
        foreach (var creature in board.Timeline.Select(slot => slot.Creature).Distinct())
        {
            var snapshot = creatures.First(other => other.Id == creature);
            spells[creature] = snapshot.Owner == board.Slot ? AllyIntent(board, snapshot) : GreedyIntent(board, snapshot, creatures);
        }

        return spells;
    }

    /// <summary>What the heuristic agent would declare for an ally that has not yet, given the team's declarations so far.</summary>
    private SpellId? AllyIntent(PlayerBoardState board, CreatureSnapshot ally)
    {
        var castable = Castable(ally);
        return castable.Count == 0 ? null : _oneStep.DecideIntent(board, new IntentOption(ally.Id, castable));
    }

    /// <summary>The spells a creature knows and can afford, in ordinal id order.</summary>
    private List<SpellId> Castable(CreatureSnapshot creature) =>
        [.. creature.KnownSpells
            .Where(spell => resources.GetSpell(spell).Stats.Cost.Value <= creature.Energy.Value)
            .OrderBy(spell => spell.Value, StringComparer.Ordinal)];

    /// <summary>The spell the scorer would declare for a creature on a board, as the greedy agent declares it.</summary>
    private SpellId? GreedyIntent(PlayerBoardState board, CreatureSnapshot snapshot, IReadOnlyList<CreatureSnapshot> creatures)
    {
        SpellId? best = null;
        var bestScore = double.NegativeInfinity;
        foreach (var spell in snapshot.KnownSpells.OrderBy(spell => spell.Value, StringComparer.Ordinal))
        {
            if (resources.GetSpell(spell).Stats.Cost.Value > snapshot.Energy.Value)
            {
                continue;
            }

            var score = _scorer.Best(snapshot, spell, creatures, speed: SpeedOf(board, snapshot.Id))?.Score;
            if (score is { } found && found > bestScore)
            {
                best = spell;
                bestScore = found;
            }
        }

        return best;
    }

    /// <summary>The spell on its best targets by the one-step reading, or nothing when it has no legal target.</summary>
    private CombatAction? BestTargets(PlayerBoardState board, IReadOnlyList<CreatureSnapshot> ahead, CreatureId actor, SpellId spell)
    {
        var snapshot = ahead.First(candidate => candidate.Id == actor);
        return _scorer.Best(snapshot, spell, ahead, speed: SpeedOf(board, actor)) is { } found
            ? CombatAction.Bind(new CombatIntent(actor, spell), found.Targets)
            : null;
    }

    /// <summary>
    /// What a creature chose in the Speed sub-phase, which decides whether its cast can crit at all (a Quick
    /// cast never does). The timeline carries every creature's, the enemy's included, so every reading of a
    /// cast -- the actor's own, and the guess of what another creature casts -- prices the roll it can
    /// actually make. The board's own choices are the fallback for a board asked before the timeline is
    /// built; Standard is the answer to one asked before the Speed sub-phase, not a creature that picked it.
    /// </summary>
    private static Speed SpeedOf(PlayerBoardState board, CreatureId creature) =>
        board.Timeline.FirstOrDefault(slot => slot.Creature == creature)?.Speed
        ?? board.SpeedChoices.FirstOrDefault(choice => choice.Creature == creature)?.Speed
        ?? Speed.Standard;

    private static List<CreatureSnapshot> Creatures(PlayerBoardState board) => [.. board.Allies, .. board.Enemies];
}

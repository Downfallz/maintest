using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// Plays whole rounds out on a hypothetical board, one agent in both seats (ADR 0094): what a purchase is
/// worth is read off the rounds it leads to, which no reading of one action can see.
/// <para>
/// The round is <c>Match.Step</c>'s, walked from the same public rules: the start of a round through
/// <see cref="Advance.StartOfRound"/>, an evolution's purchases revealed together through
/// <see cref="Advance.Buy"/>, the speeds, the turn order from <see cref="TimelineBuilder"/> and the tie
/// orders each seat gives, every intent declared before anything resolves, each slot in turn -- a slot the creature cannot
/// take fizzles unasked, as <see cref="ActionRules.CanTakeItsSlot"/> says -- then the cleanup and the outcome.
/// Every decision is the agent's, asked on the board state its seat would be shown, so a rollout plays the
/// way that agent plays the match. A sub-phase added to the round has to be added here too.
/// </para>
/// <para>
/// The dice are rolled from the source handed in, never forced plain: a package is often mostly its
/// criticals, and a plain rollout would price them at their floor. They are the rules' dice only: an agent
/// that decides at random draws from its own source, as it does in the match.
/// </para>
/// </summary>
public sealed class RoundRollout(IPlayerAgent player, ActionScorer scorer, IGameResources resources, RuleSet rules)
{
    /// <summary>
    /// The rounds from the Speed sub-phase of <paramref name="origin"/>'s round on, at most
    /// <paramref name="rounds"/> of them, on a board whose purchases for that round are already bought: the
    /// match they end, from <paramref name="origin"/>'s seat, then the scorer's sum over their actions, that
    /// seat's for and the other's against.
    /// </summary>
    public RolloutValue Play(PlayerBoardState origin, IReadOnlyList<CreatureSnapshot> board, int rounds, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(random);

        var first = origin.RoundNumber ?? 1;
        var seat = origin.Slot;
        var score = 0.0;
        for (var round = first; round < first + rounds; round++)
        {
            if (round > first)
            {
                board = Advance.StartOfRound(board, resources, rules);
                if (Advance.Elimination(board) is { } bled)
                {
                    return RolloutValue.Of(bled, seat, score);
                }

                if (rules.EvolutionPicksIn(round) > 0)
                {
                    board = Advance.Buy(board, [.. Picks(origin, board, round, PlayerSlot.Player1, []), .. Picks(origin, board, round, PlayerSlot.Player2, [])], resources);
                }
            }

            var (played, wiped) = Combat(origin, board, round, random, ref score);
            if (wiped is not null)
            {
                return RolloutValue.Of(wiped, seat, score);
            }

            board = Advance.Cleanup(played, resources);
            if (Advance.Outcome(board, resources, round, rules) is { } outcome)
            {
                return RolloutValue.Of(outcome, seat, score);
            }
        }

        return new RolloutValue(0, score);
    }

    /// <summary>
    /// The picks a seat makes at an evolution, one at a time as the match asks for them: each on the board
    /// state that seat is shown, its earlier picks of the round on it, and a creature already picked for no
    /// longer offered (ADR 0066). <paramref name="made"/> are picks the seat has made already.
    /// </summary>
    public List<EvolutionChoice> Picks(PlayerBoardState origin, IReadOnlyList<CreatureSnapshot> board, int round, PlayerSlot seat, IReadOnlyList<EvolutionChoice> made)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(made);

        List<EvolutionChoice> picks = [.. made];
        while (true)
        {
            var offers = Offers(board, seat, picks);
            var remaining = Math.Min(rules.EvolutionPicksIn(round) - picks.Count, offers.Count);
            if (remaining <= 0)
            {
                return picks;
            }

            var view = View(origin, board, seat, round, RoundSubPhase.Evolution) with { EvolutionChoices = picks };
            var decision = player.DecideEvolution(view, new EvolutionOptions(remaining, offers));
            if (decision.Choice is not { } choice)
            {
                return picks;
            }

            picks.Add(choice);
        }
    }

    /// <summary>A seat's living creatures not yet picked this round, each with the packages it can buy.</summary>
    public List<EvolutionOption> Offers(IReadOnlyList<CreatureSnapshot> board, PlayerSlot seat, IReadOnlyList<EvolutionChoice> picks) =>
    [
        .. board
            .Where(creature => creature.Owner == seat && creature.IsAlive && !picks.Any(pick => pick.Creature == creature.Id))
            .Select(creature => new EvolutionOption(creature.Id, TierEligibility.AvailableTiers(creature, resources)))
            .Where(option => option.AvailableTiers.Count > 0),
    ];

    /// <summary>
    /// A round's Planning after its evolution, and its Combat: the speeds, the timeline, every intent, then each
    /// slot. Returns the board the last slot left, and the elimination it stopped on if a team was wiped.
    /// </summary>
    private (IReadOnlyList<CreatureSnapshot> Board, MatchOutcome? Wiped) Combat(PlayerBoardState origin, IReadOnlyList<CreatureSnapshot> board, int round, IRandomSource random, ref double score)
    {
        var speeds = new Dictionary<PlayerSlot, List<SpeedChoice>> { [PlayerSlot.Player1] = [], [PlayerSlot.Player2] = [] };
        foreach (var creature in board.Where(creature => creature.IsAlive && !creature.IsStunned))
        {
            var own = speeds[creature.Owner];
            var view = View(origin, board, creature.Owner, round, RoundSubPhase.Speed) with { SpeedChoices = [.. own] };
            own.Add(new SpeedChoice(creature.Id, player.DecideSpeed(view, creature.Id)));
        }

        var timeline = Ordered(origin, board, round, TimelineBuilder.Build(board, [.. speeds[PlayerSlot.Player1], .. speeds[PlayerSlot.Player2]], random), speeds);
        var slots = timeline.Slots;
        var intents = new Dictionary<PlayerSlot, List<CombatIntent>> { [PlayerSlot.Player1] = [], [PlayerSlot.Player2] = [] };
        foreach (var slot in slots)
        {
            var actor = board.First(creature => creature.Id == slot.Creature);
            var own = intents[slot.Owner];
            var view = Planned(View(origin, board, slot.Owner, round, RoundSubPhase.IntentSelection), timeline, speeds[slot.Owner], own);
            own.Add(new CombatIntent(actor.Id, player.DecideIntent(view, new IntentOption(actor.Id, Castable(actor)))));
        }

        var seat = origin.Slot;
        List<CombatAction> revealed = [];
        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            var intent = intents[slot.Owner].First(declared => declared.Actor == slot.Creature);
            IReadOnlyList<CreatureId> targets = [];
            if (ActionRules.CanTakeItsSlot(intent, board, resources))
            {
                var actor = board.First(creature => creature.Id == slot.Creature);
                var legal = TargetingRules.LegalTargets(actor, resources.GetSpell(intent.Spell), board);
                var view = Planned(View(origin, board, slot.Owner, round, RoundSubPhase.Activation), timeline, speeds[slot.Owner], intents[slot.Owner])
                    with
                { RevealedActions = [.. revealed], ActivationCursor = index };
                targets = player.DecideTargets(view, new TargetOptions(actor.Id, intent.Spell, legal));
            }

            var action = CombatAction.Bind(intent, targets);
            revealed.Add(action);
            var advanced = Advance.Action(action, board, resources, rules, random, slot.Speed);
            var sign = slot.Owner == seat ? 1 : -1;
            var stillToAct = slots.Skip(index + 1).Select(later => later.Creature).ToHashSet();
            score += sign * scorer.Score(advanced.Resolution, board, stillToAct: stillToAct);
            board = advanced.Board;
            if (Advance.Elimination(board) is { } wiped)
            {
                return (board, wiped);
            }
        }

        return (board, null);
    }

    /// <summary>
    /// The timeline after the TieOrder sub-phase: each seat with a tie among its own creatures is asked how to
    /// seat them, as the match asks it, and the orders given are applied together (ADR 0063).
    /// </summary>
    private CombatTimeline Ordered(PlayerBoardState origin, IReadOnlyList<CreatureSnapshot> board, int round, CombatTimeline timeline, Dictionary<PlayerSlot, List<SpeedChoice>> speeds)
    {
        var orders = new Dictionary<PlayerSlot, IReadOnlyList<CreatureId>>();
        foreach (var seat in speeds.Keys.Where(seat => TieOrderRules.HasTieOrderToGive(timeline, seat)))
        {
            var view = Planned(View(origin, board, seat, round, RoundSubPhase.TieOrder), timeline, speeds[seat], []);
            var order = player.DecideTieOrder(view, new TieOrderOptions(TieOrderRules.TiesOf(timeline, seat)));
            // An order the match would refuse is not one a rollout can play; the roll stands in for it.
            if (TieOrderRules.ValidateOrder(seat, order, timeline).IsSuccess)
            {
                orders[seat] = order;
            }
        }

        return orders.Count == 0 ? timeline : TieOrderRules.Apply(timeline, orders);
    }

    /// <summary>The board state a seat is shown in a sub-phase of a round on a hypothetical board, as the match's projection builds it.</summary>
    private PlayerBoardState View(PlayerBoardState origin, IReadOnlyList<CreatureSnapshot> board, PlayerSlot seat, int round, RoundSubPhase subPhase) =>
        origin with
        {
            Slot = seat,
            State = MatchState.InProgress,
            RoundNumber = round,
            Phase = RoundFlow.PhaseOf(subPhase),
            SubPhase = subPhase,
            Allies = [.. board.Where(creature => creature.Owner == seat)],
            Enemies = [.. board.Where(creature => creature.Owner != seat)],
            EvolutionChoices = [],
            HasPassedEvolution = false,
            NextEvolutionRound = rules.NextEvolutionRound(round),
            SpeedChoices = [],
            Intents = [],
            Timeline = [],
            RollOffs = [],
            RevealedActions = [],
            ActivationCursor = 0,
            Outcome = null,
        };

    /// <summary>A view once the timeline is built: the seat's own speeds and intents, every slot and roll-off.</summary>
    private static PlayerBoardState Planned(PlayerBoardState view, CombatTimeline timeline, IReadOnlyList<SpeedChoice> speeds, IReadOnlyList<CombatIntent> intents) =>
        view with
        {
            SpeedChoices = [.. speeds],
            Intents = [.. intents],
            Timeline = timeline.Slots,
            RollOffs = timeline.RollOffs,
        };

    /// <summary>The spells a creature knows and can afford, in ordinal id order, as the match offers them.</summary>
    private List<SpellId> Castable(CreatureSnapshot creature) =>
        [.. creature.KnownSpells.Where(spell => resources.GetSpell(spell).Stats.Cost <= creature.Energy).OrderBy(spell => spell.Value, StringComparer.Ordinal)];
}

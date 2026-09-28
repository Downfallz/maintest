using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// One cycle of play: a forward-only walk through the sub-phases of <see cref="RoundFlow"/> and the store of the
/// choices made along the way. The round knows the flow and what was submitted; it knows no rule about who may
/// submit what, and nothing about creatures or spells beyond their ids. Its mutators are internal: the
/// <see cref="Match"/> aggregate is the only way to move a round forward.
/// </summary>
public sealed class Round : Entity<RoundId>
{
    private readonly Dictionary<PlayerSlot, List<EvolutionChoice>> _evolutionChoices = new()
    {
        [PlayerSlot.Player1] = [],
        [PlayerSlot.Player2] = [],
    };

    private readonly Dictionary<PlayerSlot, Dictionary<CreatureId, CombatIntent>> _intents = new()
    {
        [PlayerSlot.Player1] = [],
        [PlayerSlot.Player2] = [],
    };

    private readonly HashSet<PlayerSlot> _evolutionPasses = [];
    private readonly Dictionary<CreatureId, SpeedChoice> _speedChoices = [];
    private readonly Dictionary<CreatureId, CombatAction> _actions = [];
    private readonly Dictionary<PlayerSlot, IReadOnlyList<CreatureId>> _tieOrders = [];

    private Round(RoundId id)
        : base(id)
    {
    }

    public int Number => Id.Number;

    public RoundSubPhase SubPhase { get; private set; } = RoundFlow.First;

    public RoundPhase Phase => RoundFlow.PhaseOf(SubPhase);

    public bool IsFinalized => SubPhase == RoundFlow.Last;

    public CombatTimeline Timeline { get; private set; } = CombatTimeline.Empty;

    /// <summary>
    /// How far combat has walked the timeline: the slots before it have been revealed, targeted and resolved
    /// (ADR 0083).
    /// </summary>
    public TurnCursor ActivationCursor { get; private set; } = TurnCursor.Start;

    public bool IsCombatResolved => ActivationCursor.IsEnd(Timeline.Count);

    /// <summary>
    /// The slot whose intent is revealed, targeted and resolved next, or <c>null</c> when every slot has been.
    /// </summary>
    public ActivationSlot? NextSlot => IsCombatResolved ? null : Timeline[ActivationCursor.Index];

    public static Round First() => new(RoundId.First);

    public Round Next() => new(Id.Next());

    /// <summary>
    /// Moves to the next sub-phase. Moving past the last one is an invariant violation.
    /// </summary>
    internal void Advance()
    {
        SubPhase = RoundFlow.After(SubPhase)
            ?? throw new InvalidOperationException($"Round {Number} is finalized and cannot advance.");
    }

    public IReadOnlyList<EvolutionChoice> EvolutionChoicesOf(PlayerSlot slot) => _evolutionChoices[slot];

    internal Result SubmitEvolutionChoice(PlayerSlot slot, EvolutionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        if (SubPhase != RoundSubPhase.Evolution)
        {
            return Result.Failure(RoundErrors.EvolutionNotOpen);
        }

        // One choice a creature a round (ADR 0066). The rules refuse it first, with a code that says why; this
        // keeps the history the rule reads from ever holding two.
        var choices = _evolutionChoices[slot];
        if (choices.Any(existing => existing.Creature == choice.Creature))
        {
            return Result.Failure(RoundErrors.EvolutionAlreadySubmitted);
        }

        choices.Add(choice);
        return Result.Success();
    }

    public bool HasPassedEvolution(PlayerSlot slot) => _evolutionPasses.Contains(slot);

    /// <summary>
    /// Records that a player gives up their remaining evolution picks for this round.
    /// </summary>
    internal Result PassEvolution(PlayerSlot slot)
    {
        if (SubPhase != RoundSubPhase.Evolution)
        {
            return Result.Failure(RoundErrors.EvolutionNotOpen);
        }

        return _evolutionPasses.Add(slot) ? Result.Success() : Result.Failure(RoundErrors.EvolutionAlreadyPassed);
    }

    public IReadOnlyCollection<SpeedChoice> SpeedChoices => _speedChoices.Values;

    public SpeedChoice? SpeedChoiceOf(CreatureId creature) => _speedChoices.GetValueOrDefault(creature);

    internal Result SubmitSpeedChoice(SpeedChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        if (SubPhase != RoundSubPhase.Speed)
        {
            return Result.Failure(RoundErrors.SpeedNotOpen);
        }

        return _speedChoices.TryAdd(choice.Creature, choice)
            ? Result.Success()
            : Result.Failure(RoundErrors.SpeedAlreadyChosen);
    }

    /// <summary>
    /// Installs the timeline built by the planning rules and resets the cursor.
    /// </summary>
    internal void SetTimeline(CombatTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        RequireSubPhase(RoundSubPhase.TurnOrderResolution, "set the timeline");

        Timeline = timeline;
        ActivationCursor = TurnCursor.Start;
    }

    /// <summary>
    /// The tie orders submitted this round, by player (ADR 0063). Internal: it holds both seats' hidden orders
    /// at once, and only the match applies them; a seat reads its own with <see cref="TieOrderOf"/>.
    /// </summary>
    internal IReadOnlyDictionary<PlayerSlot, IReadOnlyList<CreatureId>> TieOrders => _tieOrders;

    public IReadOnlyList<CreatureId>? TieOrderOf(PlayerSlot slot) => _tieOrders.GetValueOrDefault(slot);

    internal Result SubmitTieOrder(PlayerSlot slot, IReadOnlyList<CreatureId> order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (SubPhase != RoundSubPhase.TieOrder)
        {
            return Result.Failure(RoundErrors.TieOrderNotOpen);
        }

        return _tieOrders.TryAdd(slot, [.. order])
            ? Result.Success()
            : Result.Failure(RoundErrors.TieOrderAlreadySubmitted);
    }

    /// <summary>
    /// Installs the timeline the tie orders produced. The same slots, and every place keeps its side, its speed
    /// and its initiative: a tie order moves a player's creatures between their own places in one tie and
    /// nothing else (ADR 0063). The cursor stays at the start, since nothing has been revealed yet.
    /// </summary>
    internal void ReorderTimeline(CombatTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        RequireSubPhase(RoundSubPhase.TieOrder, "reorder the timeline");

        var samePlaces = timeline.Count == Timeline.Count
            && timeline.Slots.All(Timeline.Slots.Contains)
            && timeline.Slots.Zip(Timeline.Slots).All(pair => pair.First.Owner == pair.Second.Owner && pair.First.TiesWith(pair.Second));
        if (!samePlaces)
        {
            throw new InvalidOperationException($"Round {Number}: a tie order moves a player's creatures between their own places in a tie, and nothing else.");
        }

        Timeline = timeline;
    }

    /// <summary>
    /// The intents a player submitted. Intents stay hidden from the other player until revealed.
    /// </summary>
    public IReadOnlyCollection<CombatIntent> IntentsOf(PlayerSlot slot) => _intents[slot].Values;

    public bool HasIntent(CreatureId creature) => IntentOf(creature) is not null;

    public CombatIntent? IntentOf(CreatureId creature) =>
        _intents[PlayerSlot.Player1].GetValueOrDefault(creature) ?? _intents[PlayerSlot.Player2].GetValueOrDefault(creature);

    internal Result SubmitIntent(PlayerSlot slot, CombatIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (SubPhase != RoundSubPhase.IntentSelection)
        {
            return Result.Failure(RoundErrors.IntentsNotOpen);
        }

        if (HasIntent(intent.Actor))
        {
            return Result.Failure(RoundErrors.IntentAlreadySubmitted);
        }

        _intents[slot].Add(intent.Actor, intent);
        return Result.Success();
    }

    /// <summary>
    /// The intent at the cursor, without moving it. The caller binds targets with <see cref="SubmitAction"/>.
    /// </summary>
    public CombatIntent? PeekNextIntent() =>
        NextSlot is { } slot ? IntentOf(slot.Creature) : null;

    public CombatAction? ActionOf(CreatureId creature) => _actions.GetValueOrDefault(creature);

    /// <summary>
    /// Accepts the targeted action for the intent at the cursor. The cursor moves once the match has resolved
    /// it, with <see cref="MarkSlotActivated"/>.
    /// </summary>
    internal Result SubmitAction(CombatAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (SubPhase != RoundSubPhase.Activation)
        {
            return Result.Failure(RoundErrors.TargetingNotOpen);
        }

        if (NextSlot is not { } slot)
        {
            return Result.Failure(RoundErrors.NothingLeftToReveal);
        }

        if (slot.Creature != action.Actor)
        {
            return Result.Failure(RoundErrors.NotThisCreaturesTurn);
        }

        var intent = IntentOf(slot.Creature)
            ?? throw new InvalidOperationException($"Creature {slot.Creature} is on the timeline without an intent.");

        if (intent.Spell != action.Spell)
        {
            return Result.Failure(RoundErrors.ActionDoesNotMatchIntent);
        }

        _actions[action.Actor] = action;
        return Result.Success();
    }

    /// <summary>
    /// Moves the cursor past the slot whose action the match has just resolved. A slot without a bound action
    /// and a wrong sub-phase are invariant violations.
    /// </summary>
    internal void MarkSlotActivated()
    {
        RequireSubPhase(RoundSubPhase.Activation, "activate a slot");

        var slot = NextSlot
            ?? throw new InvalidOperationException($"Round {Number}: combat is already resolved.");
        if (!_actions.ContainsKey(slot.Creature))
        {
            throw new InvalidOperationException($"Creature {slot.Creature} is activated without a bound action.");
        }

        ActivationCursor = ActivationCursor.MoveNext();
    }

    public override string ToString() => $"Round {Number} ({Phase}/{SubPhase})";

    private void RequireSubPhase(RoundSubPhase expected, string operation)
    {
        if (SubPhase != expected)
        {
            throw new InvalidOperationException($"Round {Number}: cannot {operation} during {SubPhase}; expected {expected}.");
        }
    }
}

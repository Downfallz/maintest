using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// One cycle of play: a forward-only walk through the sub-phases of <see cref="RoundFlow"/> and the store of the
/// choices made along the way. The round knows the flow and what was submitted; it knows no rule about who may
/// submit what, and nothing about creatures or spells beyond their ids.
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

    private readonly Dictionary<CreatureId, SpeedChoice> _speedChoices = [];
    private readonly Dictionary<CreatureId, CombatAction> _actions = [];

    private Round(RoundId id)
        : base(id)
    {
    }

    public int Number => Id.Number;

    public RoundSubPhase SubPhase { get; private set; } = RoundFlow.First;

    public RoundPhase Phase => RoundFlow.PhaseOf(SubPhase);

    public bool IsFinalized => SubPhase == RoundFlow.Last;

    public CombatTimeline Timeline { get; private set; } = CombatTimeline.Empty;

    public TurnCursor RevealCursor { get; private set; } = TurnCursor.Start;

    public TurnCursor ResolveCursor { get; private set; } = TurnCursor.Start;

    public bool AllActionsBound => RevealCursor.IsEnd(Timeline.Count);

    public bool IsCombatResolved => ResolveCursor.IsEnd(Timeline.Count);

    /// <summary>
    /// The slot whose intent is revealed and targeted next, or <c>null</c> when every slot has been.
    /// </summary>
    public ActivationSlot? NextSlotToReveal => AllActionsBound ? null : Timeline[RevealCursor.Index];

    /// <summary>
    /// The slot whose action resolves next, or <c>null</c> when combat is resolved.
    /// </summary>
    public ActivationSlot? NextSlotToResolve => IsCombatResolved ? null : Timeline[ResolveCursor.Index];

    public static Round First() => new(RoundId.First);

    public Round Next() => new(Id.Next());

    /// <summary>
    /// Moves to the next sub-phase. Moving past the last one is an invariant violation.
    /// </summary>
    public void Advance()
    {
        SubPhase = RoundFlow.After(SubPhase)
            ?? throw new InvalidOperationException($"Round {Number} is finalized and cannot advance.");
    }

    public IReadOnlyList<EvolutionChoice> EvolutionChoicesOf(PlayerSlot slot) => _evolutionChoices[slot];

    public Result SubmitEvolutionChoice(PlayerSlot slot, EvolutionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        if (SubPhase != RoundSubPhase.Evolution)
        {
            return Result.Failure(RoundErrors.EvolutionNotOpen);
        }

        var choices = _evolutionChoices[slot];
        if (choices.Contains(choice))
        {
            return Result.Failure(RoundErrors.EvolutionAlreadySubmitted);
        }

        choices.Add(choice);
        return Result.Success();
    }

    public IReadOnlyCollection<SpeedChoice> SpeedChoices => _speedChoices.Values;

    public SpeedChoice? SpeedChoiceOf(CreatureId creature) => _speedChoices.GetValueOrDefault(creature);

    public Result SubmitSpeedChoice(SpeedChoice choice)
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
    /// Installs the timeline built by the planning rules and resets both cursors.
    /// </summary>
    public void SetTimeline(CombatTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        RequireSubPhase(RoundSubPhase.TurnOrderResolution, "set the timeline");

        Timeline = timeline;
        RevealCursor = TurnCursor.Start;
        ResolveCursor = TurnCursor.Start;
    }

    /// <summary>
    /// The intents a player submitted. Intents stay hidden from the other player until revealed.
    /// </summary>
    public IReadOnlyCollection<CombatIntent> IntentsOf(PlayerSlot slot) => _intents[slot].Values;

    public bool HasIntent(CreatureId creature) => IntentOf(creature) is not null;

    public CombatIntent? IntentOf(CreatureId creature) =>
        _intents[PlayerSlot.Player1].GetValueOrDefault(creature) ?? _intents[PlayerSlot.Player2].GetValueOrDefault(creature);

    public Result SubmitIntent(PlayerSlot slot, CombatIntent intent)
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
    /// The intent at the reveal cursor, without moving it. The caller binds targets with <see cref="SubmitAction"/>.
    /// </summary>
    public CombatIntent? PeekNextIntent() =>
        NextSlotToReveal is { } slot ? IntentOf(slot.Creature) : null;

    public CombatAction? ActionOf(CreatureId creature) => _actions.GetValueOrDefault(creature);

    /// <summary>
    /// Accepts the targeted action for the intent at the reveal cursor and moves the cursor forward.
    /// </summary>
    public Result SubmitAction(CombatAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (SubPhase != RoundSubPhase.RevealAndTarget)
        {
            return Result.Failure(RoundErrors.TargetingNotOpen);
        }

        if (NextSlotToReveal is not { } slot)
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
        RevealCursor = RevealCursor.MoveNext();
        return Result.Success();
    }

    /// <summary>
    /// The action at the resolve cursor. Missing actions and wrong sub-phases are invariant violations.
    /// </summary>
    public CombatAction NextActionToResolve()
    {
        RequireSubPhase(RoundSubPhase.ActionResolution, "resolve an action");

        var slot = NextSlotToResolve
            ?? throw new InvalidOperationException($"Round {Number}: combat is already resolved.");

        return _actions.GetValueOrDefault(slot.Creature)
            ?? throw new InvalidOperationException($"Creature {slot.Creature} is on the timeline without a bound action.");
    }

    public void MarkActionResolved()
    {
        RequireSubPhase(RoundSubPhase.ActionResolution, "mark an action resolved");

        if (IsCombatResolved)
        {
            throw new InvalidOperationException($"Round {Number}: combat is already resolved.");
        }

        ResolveCursor = ResolveCursor.MoveNext();
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

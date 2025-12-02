using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Messages;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.Phases;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;
using System.Collections.ObjectModel;

namespace DA.Game.Domain2.Matches.Entities
{
    /// <summary>
    /// Round is a pure state + state-machine holder:
    /// - Knows phases & subphases.
    /// - Stores choices (evolution, speed, intents, combat actions).
    /// - Does NOT know ruleset / max counts / resources.
    /// </summary>
    public sealed class Round : Entity<RoundId>
    {
        // --------------------
        // Lifecycle
        // --------------------

        public RoundLifecycle Lifecycle { get; } = new();
        public RoundSubPhaseLifecycle SubLifecycle { get; } = new();

        public RoundPhase Phase => Lifecycle.Phase;
        public RoundSubPhase? SubPhase => SubLifecycle.SubPhase;

        // --------------------
        // Timeline + cursors
        // --------------------

        /// <summary>
        /// The combat activation order for this round.
        /// </summary>
        public CombatTimeline Timeline { get; private set; } = CombatTimeline.Empty;

        /// <summary>
        /// Cursor used during the reveal/target selection pass.
        /// </summary>
        public TurnCursor RevealCursor { get; private set; } = TurnCursor.Start;

        /// <summary>
        /// Cursor used during the combat resolution pass.
        /// </summary>
        public TurnCursor ResolveCursor { get; private set; } = TurnCursor.Start;

        // --------------------
        // Stored decisions
        // --------------------

        private readonly Dictionary<CreatureId, CombatActionChoice> _combatActionsByCreature = new();
        private readonly Dictionary<CreatureId, CombatActionIntent> _combatIntentsByCreature = new();

        private readonly HashSet<SpellUnlockChoice> _player1EvolutionChoices = new();
        private readonly HashSet<SpellUnlockChoice> _player2EvolutionChoices = new();

        private readonly HashSet<SpeedChoice> _player1SpeedChoices = new();
        private readonly HashSet<SpeedChoice> _player2SpeedChoices = new();

        public int Number { get; }

        public IReadOnlyCollection<SpellUnlockChoice> Player1EvolutionChoices => _player1EvolutionChoices;
        public IReadOnlyCollection<SpellUnlockChoice> Player2EvolutionChoices => _player2EvolutionChoices;

        public IReadOnlyCollection<SpeedChoice> Player1SpeedChoices => _player1SpeedChoices;
        public IReadOnlyCollection<SpeedChoice> Player2SpeedChoices => _player2SpeedChoices;

        public ReadOnlyDictionary<CreatureId, CombatActionChoice> CombatActionsByCreature =>
            _combatActionsByCreature.AsReadOnly();

        public ReadOnlyDictionary<CreatureId, CombatActionIntent> CombatIntentsByCreature =>
            _combatIntentsByCreature.AsReadOnly();

        public bool IsCombatResolutionCompleted { get; private set; }

        // --------------------
        // Ctor / factories
        // --------------------

        private Round(RoundId id) : base(id)
        {
            Number = id.Value;
        }

        public static Round StartFirst() =>
            new(RoundId.New(1));

        public static Round StartNext(Round previous)
        {
            ArgumentNullException.ThrowIfNull(previous);
            return new Round(RoundId.New(previous.Number + 1));
        }

        // --------------------
        // Evolution phase
        // --------------------

        public Result InitializeEvolutionPhase()
        {
            var phaseTransition = Lifecycle.MoveTo(RoundPhase.Planning);
            if (!phaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

            var subPhaseTransition = SubLifecycle.MoveTo(RoundPhase.Planning, RoundSubPhase.Planning_Evolution);
            if (!subPhaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

            return Result.Ok();
        }

        public Result SubmitEvolutionChoice(CreaturePerspective perspective, SpellUnlockChoice choice)
        {
            ArgumentNullException.ThrowIfNull(perspective);
            ArgumentNullException.ThrowIfNull(choice);

            if (Phase != RoundPhase.Planning || SubPhase != RoundSubPhase.Planning_Evolution)
                return Result.Fail(RoundErrorCodes.D101_INVALID_PHASE_EVOLUTION);

            var targetSet = perspective.Actor.OwnerSlot == PlayerSlot.Player1
                ? _player1EvolutionChoices
                : _player2EvolutionChoices;

            // Only uniqueness per creature – max counts are enforced by Match/RuleSet.
            if (!targetSet.Add(choice))
                return Result.Fail(RoundErrorCodes.D102_EVOLUTION_ALREADY_SUBMITTED);

            return Result.Ok();
        }

        // --------------------
        // Speed phase
        // --------------------

        public Result InitializeSpeedPhase()
        {
            var phaseTransition = Lifecycle.MoveTo(RoundPhase.Planning);
            if (!phaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

            var subPhaseTransition = SubLifecycle.MoveTo(RoundPhase.Planning, RoundSubPhase.Planning_Speed);
            if (!subPhaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

            return Result.Ok();
        }

        public Result SubmitSpeedChoice(CreaturePerspective perspective, SpeedChoice choice)
        {
            ArgumentNullException.ThrowIfNull(perspective);
            ArgumentNullException.ThrowIfNull(choice);

            if (Phase != RoundPhase.Planning || SubPhase != RoundSubPhase.Planning_Speed)
                return Result.Fail(RoundErrorCodes.D201_INVALID_PHASE_SPEED);

            var targetSet = perspective.Actor.OwnerSlot == PlayerSlot.Player1
                ? _player1SpeedChoices
                : _player2SpeedChoices;

            if (!targetSet.Add(choice))
                return Result.Fail(RoundErrorCodes.D202_SPEED_ALREADY_SUBMITTED);

            return Result.Ok();
        }

        public Result InitializeTurnOrderResolution(CombatTimeline timeline)
        {
            ArgumentNullException.ThrowIfNull(timeline);

            var subPhaseTransition = SubLifecycle.MoveTo(RoundPhase.Planning, RoundSubPhase.Planning_TurnOrderResolution);
            if (!subPhaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

            Timeline = timeline;
            RevealCursor = TurnCursor.Start;
            ResolveCursor = TurnCursor.Start;

            return Result.Ok();
        }

        // --------------------
        // Combat – initialization
        // --------------------

        public Result InitializeCombatPhase()
        {
            var phaseTransition = Lifecycle.MoveTo(RoundPhase.Combat);
            if (!phaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

            var subPhaseTransition = SubLifecycle.InitializeForPhase(RoundPhase.Combat);
            if (!subPhaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

            return Result.Ok();
        }

        // --------------------
        // Combat intents (spell without targets)
        // --------------------

        public Result<CombatActionIntent> SubmitCombatIntent(CreaturePerspective perspective, CombatActionIntent intent)
        {
            ArgumentNullException.ThrowIfNull(perspective);
            ArgumentNullException.ThrowIfNull(intent);

            if (Phase != RoundPhase.Combat || SubPhase != RoundSubPhase.Combat_IntentSelection)
                return Result<CombatActionIntent>.Fail(RoundErrorCodes.D301_INVALID_PHASE_INTENT);

            if (_combatIntentsByCreature.ContainsKey(intent.ActorId))
                return Result<CombatActionIntent>.Fail(RoundErrorCodes.D302_INTENT_ALREADY_SUBMITTED);

            _combatIntentsByCreature[intent.ActorId] = intent;

            return Result<CombatActionIntent>.Ok(intent);
        }

        // --------------------
        // Combat actions (spell + targets)
        // --------------------

        public Result InitializeCombatReveal()
        {
            var subPhaseTransition = SubLifecycle.MoveTo(RoundPhase.Combat, RoundSubPhase.Combat_RevealAndTarget);
            if (!subPhaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

            return Result.Ok();
        }

        public Result<CombatActionChoice> SubmitCombatAction(CreaturePerspective perspective, CombatActionChoice choice)
        {
            ArgumentNullException.ThrowIfNull(perspective);
            ArgumentNullException.ThrowIfNull(choice);

            if (Phase != RoundPhase.Combat || SubPhase != RoundSubPhase.Combat_RevealAndTarget)
                return Result<CombatActionChoice>.Fail(RoundErrorCodes.D351_INVALID_PHASE_COMBAT_ACTION);

            if (_combatActionsByCreature.ContainsKey(choice.ActorId))
                return Result<CombatActionChoice>.Fail(RoundErrorCodes.D352_COMBAT_ACTION_ALREADY_SUBMITTED);

            _combatActionsByCreature[choice.ActorId] = choice;

            return Result<CombatActionChoice>.Ok(choice);
        }

        // --------------------
        // Pass 1 : reveal intents in timeline order
        // --------------------

        public Result<CombatActionIntent> SelectNextIntentToReveal()
        {
            if (Phase != RoundPhase.Combat || SubPhase != RoundSubPhase.Combat_RevealAndTarget)
                return Result<CombatActionIntent>.InvariantFail(RoundErrorCodes.I301_INVALID_PHASE_FOR_REVEAL);

            if (Timeline.Slots.Count == 0)
                return Result<CombatActionIntent>.InvariantFail(RoundErrorCodes.I401_TIMELINE_NOT_INITIALIZED);

            if (RevealCursor.IsEnd(Timeline.Slots.Count))
                return Result<CombatActionIntent>.InvariantFail(RoundErrorCodes.I402_ALL_INTENTS_ALREADY_REVEALED);

            var slot = Timeline.Slots[RevealCursor.Index];

            if (!_combatIntentsByCreature.TryGetValue(slot.CreatureId, out var intent))
                return Result<CombatActionIntent>.InvariantFail(RoundErrorCodes.I403_NO_INTENT_FOR_CREATURE);

            RevealCursor = RevealCursor.MoveNext();

            return Result<CombatActionIntent>.Ok(intent);
        }

        // --------------------
        // Pass 2 : resolve actions in the same order
        // --------------------

        public Result InitializeCombatResolution()
        {
            var subPhaseTransition = SubLifecycle.MoveTo(RoundPhase.Combat, RoundSubPhase.Combat_ActionResolution);
            if (!subPhaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

            return Result.Ok();
        }

        public Result<CombatActionChoice> SelectNextActionToResolve()
        {
            if (Phase != RoundPhase.Combat || SubPhase != RoundSubPhase.Combat_ActionResolution)
                return Result<CombatActionChoice>.InvariantFail(RoundErrorCodes.I302_INVALID_PHASE_FOR_RESOLUTION);

            if (Timeline.Slots.Count == 0)
                return Result<CombatActionChoice>.InvariantFail(RoundErrorCodes.I401_TIMELINE_NOT_INITIALIZED);

            if (ResolveCursor.IsEnd(Timeline.Slots.Count))
                return Result<CombatActionChoice>.InvariantFail(RoundErrorCodes.I404_COMBAT_RESOLUTION_ALREADY_COMPLETED);

            var slot = Timeline.Slots[ResolveCursor.Index];

            if (!_combatActionsByCreature.TryGetValue(slot.CreatureId, out var choice))
                return Result<CombatActionChoice>.InvariantFail(RoundErrorCodes.I405_NO_ACTION_FOR_CREATURE);

            return Result<CombatActionChoice>.Ok(choice);
        }

        public Result AdvanceActionResolutionCursor()
        {
            if (Timeline.Slots.Count == 0)
                return Result.InvariantFail(RoundErrorCodes.I401_TIMELINE_NOT_INITIALIZED);

            if (ResolveCursor.IsEnd(Timeline.Slots.Count))
                return Result.InvariantFail(RoundErrorCodes.I404_COMBAT_RESOLUTION_ALREADY_COMPLETED);

            ResolveCursor = ResolveCursor.MoveNext();

            if (ResolveCursor.IsEnd(Timeline.Slots.Count))
                IsCombatResolutionCompleted = true;

            return Result.Ok();
        }

        // --------------------
        // End of round
        // --------------------

        public Result MoveToEndOfRoundCleanup()
        {
            var phaseTransition = Lifecycle.MoveTo(RoundPhase.EndOfRound);
            if (!phaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I001_INVALID_PHASE_TRANSITION);

            var subPhaseTransition = SubLifecycle.InitializeForPhase(RoundPhase.EndOfRound);
            if (!subPhaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I501_INVALID_PHASE_FOR_ROUND_FINALIZATION);

            return Result.Ok();
        }

        public Result MarkEndOfRoundFinalized()
        {
            if (Phase != RoundPhase.EndOfRound || SubPhase != RoundSubPhase.End_Cleanup)
                return Result.InvariantFail(RoundErrorCodes.I501_INVALID_PHASE_FOR_ROUND_FINALIZATION);

            var subPhaseTransition = SubLifecycle.MoveTo(RoundPhase.EndOfRound, RoundSubPhase.End_Finalization);
            if (!subPhaseTransition.IsSuccess)
                return Result.InvariantFail(RoundErrorCodes.I501_INVALID_PHASE_FOR_ROUND_FINALIZATION);

            return Result.Ok();
        }
    }
}

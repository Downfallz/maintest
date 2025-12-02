using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Events.Combat;
using DA.Game.Domain2.Matches.Events.EndOfRound;
using DA.Game.Domain2.Matches.Events.Match;
using DA.Game.Domain2.Matches.Events.Planning;
using DA.Game.Domain2.Matches.Events.StartOfRound;
using DA.Game.Domain2.Matches.Messages;
using DA.Game.Domain2.Matches.RuleSets;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.Phases;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Players;
using DA.Game.Shared.Contracts.Players.Ids;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Domain2.Matches.Aggregates
{
    /// <summary>
    /// Represents a match between two players.
    /// Orchestrates the lifecycle: join, rounds, evolutions, turns.
    /// </summary>
    public sealed class Match : AggregateRoot<MatchId>
    {
        private readonly IGameResources _resources;
        private readonly RuleSet _ruleSet;
        private readonly IClock _clock;

        public MatchLifecycle Lifecycle { get; } = new();

        public MatchState State => Lifecycle.State;

        public PlayerRef? PlayerRef1 { get; private set; }
        public PlayerRef? PlayerRef2 { get; private set; }

        public Team? Player1Team { get; private set; }
        public Team? Player2Team { get; private set; }

        public Round? CurrentRound { get; private set; }
        public int RoundNumber => CurrentRound?.Number ?? 0;

        public IReadOnlyList<CombatCreature> AllCreatures =>
            (Player1Team?.Characters ?? Enumerable.Empty<CombatCreature>())
                .Concat(Player2Team?.Characters ?? Enumerable.Empty<CombatCreature>())
                .ToList();

        private Match(MatchId id, IGameResources resources, RuleSet ruleSet, IClock clock) : base(id)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _ruleSet = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>
        /// Creates a new match with the provided game resources.
        /// </summary>
        public static Match Create(IGameResources resources, RuleSet ruleSet, IClock clock)
        {
            ArgumentNullException.ThrowIfNull(resources);
            ArgumentNullException.ThrowIfNull(ruleSet);
            ArgumentNullException.ThrowIfNull(clock);

            return new Match(MatchId.New(), resources, ruleSet, clock);
        }

        /// <summary>
        /// Lets a player join the match. The match auto-starts when both players are present.
        /// </summary>
        public Result<PlayerSlot> Join(PlayerRef player)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (State != MatchState.WaitingForPlayers)
                return Result<PlayerSlot>.Fail(MatchErrors.AlreadyStarted);

            if (PlayerRef1?.Id == player.Id || PlayerRef2?.Id == player.Id)
                return Result<PlayerSlot>.Fail(MatchErrors.PlayerAlreadyInGame);

            PlayerSlot assignedSlot = default;

            if (PlayerRef1 is null)
            {
                PlayerRef1 = player;
                AddEvent(new PlayerJoined(Id, player.Id, _clock.UtcNow));
                assignedSlot = PlayerSlot.Player1;
            }
            else if (PlayerRef2 is null)
            {
                PlayerRef2 = player;
                AddEvent(new PlayerJoined(Id, player.Id, _clock.UtcNow));
                assignedSlot = PlayerSlot.Player2;
            }

            if (PlayerRef1 is not null && PlayerRef2 is not null)
                AddEvent(new AllPlayersReady(Id, _clock.UtcNow));

            return Result<PlayerSlot>.Ok(assignedSlot);
        }

        public void StartMatch()
        {
            InitializeTeams();

            Lifecycle.MoveTo(MatchState.Started);
            AddEvent(new MatchStarted(Id, _clock.UtcNow));

            InitializeFirstRound();
        }

        private void InitializeTeams()
        {
            var creatureTemplate = _resources.GetCreature(CreatureDefId.New("creature:main:v1"));

            Player1Team = Team.FromCharacterTemplateAndSlot(creatureTemplate, PlayerSlot.Player1);
            Player2Team = Team.FromCharacterTemplateAndSlot(creatureTemplate, PlayerSlot.Player2);
        }

        private void InitializeFirstRound()
        {
            CurrentRound = Round.StartFirst();
            InitializeRound();
        }

        public void StartNextRound()
        {
            ArgumentNullException.ThrowIfNull(CurrentRound);

            CurrentRound = Round.StartNext(CurrentRound);
            InitializeRound();
        }

        private void InitializeRound()
        {
            // TODO: round start effects (+2 Energy, conditions, etc.) will be handled here later.

            AddEvent(new RoundInitialized(Id, CurrentRound!.Id, RoundNumber, _clock.UtcNow));
            InitializeCurrentRoundEvolution();
        }

        private void InitializeCurrentRoundEvolution()
        {
            var result = CurrentRound!.InitializeEvolutionPhase();
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Error);
        }

        // --------------------
        // Evolution
        // --------------------

        /// <summary>
        /// Submits an evolution (spell unlock) choice for the current round.
        /// </summary>
        public Result SubmitEvolutionChoice(PlayerSlot playerSlot, SpellUnlockChoice choice)
        {
            ArgumentNullException.ThrowIfNull(choice);

            var guard = _ruleSet.Phase.MatchPhase.EnsureCanSubmitEvolutionChoice(this);
            if (!guard.IsSuccess)
                return guard;

            var ctxResult = BuildGameContextForCurrentCreature(choice.CharacterId);
            if (!ctxResult.IsSuccess)
                return Result.Fail(ctxResult.Error!);

            var roundResult = CurrentRound!.SubmitEvolutionChoice(ctxResult.Value!, choice);
            if (!roundResult.IsSuccess)
                return roundResult;

            AddEvent(new EvolutionChoiceSubmitted(
                CurrentRound.Id,
                ctxResult.Value!.Actor.OwnerSlot,
                choice,
                _clock.UtcNow));

            if (IsEvolutionPhaseComplete(CurrentRound))
            {
                AddEvent(new EvolutionPhaseCompleted(
                    Id,
                    CurrentRound.Id,
                    RoundNumber,
                    _clock.UtcNow));
            }

            return roundResult;
        }

        private static bool IsEvolutionPhaseComplete(Round round) =>
            round.Player1EvolutionChoices.Count == 2 &&
            round.Player2EvolutionChoices.Count == 2;

        // --------------------
        // Speed
        // --------------------

        public Result InitializeCurrentRoundSpeedPhase()
        {
            return CurrentRound!.InitializeSpeedPhase();
        }

        /// <summary>
        /// Submits the speed choice (Quick/Standard) for the current round.
        /// </summary>
        public Result SubmitSpeedChoice(PlayerSlot playerSlot, SpeedChoice speedChoice)
        {
            ArgumentNullException.ThrowIfNull(speedChoice);

            var guard = _ruleSet.Phase.MatchPhase.EnsureCanSubmitSpeedChoice(this);
            if (!guard.IsSuccess)
                return guard;

            var ctxResult = BuildGameContextForCurrentCreature(speedChoice.CreatureId);
            if (!ctxResult.IsSuccess)
                return Result.Fail(ctxResult.Error!);

            var roundResult = CurrentRound!.SubmitSpeedChoice(ctxResult.Value!, speedChoice);
            if (!roundResult.IsSuccess)
                return roundResult;

            AddEvent(new SpeedChoiceSubmitted(
                CurrentRound.Id,
                playerSlot,
                speedChoice,
                _clock.UtcNow));

            if (IsSpeedPhaseComplete(CurrentRound))
            {
                AddEvent(new SpeedPhaseCompleted(
                    Id,
                    CurrentRound.Id,
                    RoundNumber,
                    _clock.UtcNow));
                InitializeCurrentRoundSpeedResolutionPhase();
                InitializeCurrentRoundCombatPhase();
            }

            return roundResult;
        }

        private static bool IsSpeedPhaseComplete(Round round) =>
            round.Player1SpeedChoices.Count == 3 &&
            round.Player2SpeedChoices.Count == 3;

        private void InitializeCurrentRoundSpeedResolutionPhase()
        {
            var timeline = _ruleSet.Planning.BuildTimeline(
                Player1Team!,
                CurrentRound!.Player1SpeedChoices,
                Player2Team!,
                CurrentRound.Player2SpeedChoices);

            var result = CurrentRound.InitializeTurnOrderResolution(timeline);
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Error);

            AddEvent(new SpeedResolutionCompleted(
                Id,
                CurrentRound.Id,
                RoundNumber,
                _clock.UtcNow));
        }

        // --------------------
        // Combat – intents
        // --------------------

        private Result InitializeCurrentRoundCombatPhase()
        {
            CurrentRound!.InitializeCombatPhase();
            return Result.Ok();
        }

        public Result SubmitCombatIntent(CombatActionIntent intent)
        {
            ArgumentNullException.ThrowIfNull(intent);

            var guard = _ruleSet.Phase.MatchPhase.EnsureCanSubmitCombatAction(this);
            if (!guard.IsSuccess)
                return guard;

            var ctxResult = BuildGameContextForCurrentCreature(intent.ActorId);
            if (!ctxResult.IsSuccess)
                return Result.Fail(ctxResult.Error!);

            var validateResult = _ruleSet.Combat.ValidateIntent(ctxResult.Value!, intent);
            if (!validateResult.IsSuccess)
                return validateResult;

            var roundResult = CurrentRound!.SubmitCombatIntent(ctxResult.Value!, intent);
            if (!roundResult.IsSuccess)
                return Result.Fail(roundResult.Error!);

            AddEvent(new CombatActionIntentSubmitted(
                CurrentRound.Id,
                intent,
                _clock.UtcNow));

            // When all intents are submitted, we transition to speed resolution / timeline.
            if (IsIntentPhaseComplete(CurrentRound))
            {
                InitializeCurrentRoundCombatReveal();
                AddEvent(new CombatActionSubmitIntentCompleted(
                    Id,
                    CurrentRound.Id,
                    RoundNumber,
                    _clock.UtcNow));


            }

            return Result.Ok();
        }
        private static bool IsIntentPhaseComplete(Round round) =>
            round.CombatIntentsByCreature.Count == 6;
        // --------------------
        // Combat – reveal + bind targets
        // --------------------
        private Result InitializeCurrentRoundCombatReveal()
        {
            CurrentRound!.InitializeCombatReveal();
            return Result.Ok();
        }
        public Result RevealNextActionAndBindTargets(IReadOnlyList<CreatureId> targetIds)
        {
            ArgumentNullException.ThrowIfNull(targetIds);

            if (CurrentRound is null)
                return Result.Fail("No active round.");

            var nextIntentResult = CurrentRound.SelectNextIntentToReveal();
            if (!nextIntentResult.IsSuccess)
                return Result.Fail(nextIntentResult.Error!);

            var intent = nextIntentResult.Value!;

            var ctxResult = BuildGameContextForCurrentCreature(intent.ActorId);
            if (!ctxResult.IsSuccess)
                return Result.Fail(ctxResult.Error!);

            var actionChoice = CombatActionChoice.FromIntentAndTargets(intent, targetIds);

            var validateResult = _ruleSet.Combat.ValidateAction(ctxResult.Value!, actionChoice);
            if (!validateResult.IsSuccess)
                return validateResult;

            var submitResult = CurrentRound.SubmitCombatAction(ctxResult.Value!, actionChoice);
            if (!submitResult.IsSuccess)
                return Result.Fail(submitResult.Error!);

            AddEvent(new CombatActionChoiceSubmitted(
                CurrentRound.Id,
                actionChoice,
                _clock.UtcNow));

            if (IsCombatActionSubmissionCompleted(CurrentRound))
            {
                InitializeCurrentRoundCombatResolution();
                AddEvent(new CombatActionRequestPhaseCompleted(
                    Id,
                    CurrentRound.Id,
                    RoundNumber,
                    _clock.UtcNow));
            }

            return Result.Ok();
        }
        private static bool IsCombatActionSubmissionCompleted(Round round) =>
            round.CombatActionsByCreature.Count == 6;
        private Result InitializeCurrentRoundCombatResolution()
        {
            CurrentRound!.InitializeCombatResolution();
            return Result.Ok();
        }

        // --------------------
        // Combat – resolution
        // --------------------

        public Result<bool> ResolveNextCombatStep()
        {
            if (CurrentRound is null)
                return Result<bool>.Fail("No active round.");

            var nextActionResult = CurrentRound.SelectNextActionToResolve();
            if (!nextActionResult.IsSuccess)
                return Result<bool>.Fail(nextActionResult.Error!);

            var actionChoice = nextActionResult.Value!;

            var ctxResult = BuildGameContextForCurrentCreature(actionChoice.ActorId);
            if (!ctxResult.IsSuccess)
                return Result<bool>.Fail(ctxResult.Error!);

            // 1) Compute the combat result (damage, crit, etc.)
            var resolutionResult = _ruleSet.Combat.Resolve(ctxResult.Value!, actionChoice);
            if (!resolutionResult.IsSuccess)
                return Result<bool>.Fail(resolutionResult.Error!);

            // 2) Apply the combat result on the creatures
            var applyResult = _ruleSet.Combat.ApplyCombatResult(resolutionResult.Value!, AllCreatures);
            if (!applyResult.IsSuccess)
                return Result<bool>.Fail(applyResult.Error!);

            // 3) Event could be raised here (currently commented out in your version)

            var cursorResult = CurrentRound.AdvanceActionResolutionCursor();
            if (!cursorResult.IsSuccess)
                return Result<bool>.Fail(cursorResult.Error!);

            // 4) If resolution is done, finalize the round.
            if (CurrentRound.IsCombatResolutionCompleted)
            {
                AddEvent(new CombatResolutionCompleted(
                    Id,
                    CurrentRound.Id,
                    RoundNumber,
                    _clock.UtcNow));

                FinalizeCurrentRound();
                return Result<bool>.Ok(true);
            }

            return Result<bool>.Ok(false);
        }

        private void FinalizeCurrentRound()
        {
            ArgumentNullException.ThrowIfNull(CurrentRound);

            // 1) End-of-round cleanup (conditions, etc.)
            var cleanupResult = CurrentRound.MoveToEndOfRoundCleanup();
            if (!cleanupResult.IsSuccess)
                throw new InvalidOperationException(cleanupResult.Error);

            // TODO: apply end-of-round effects here (conditions, etc.)

            // 2) Is the match finished?
            if (TryEndMatchIfNeeded())
            {
                CurrentRound.MarkEndOfRoundFinalized();
                return;
            }

            // 3) Round ended but match continues
            AddEvent(new RoundEnded(
                Id,
                CurrentRound.Id,
                RoundNumber,
                _clock.UtcNow));

            CurrentRound.MarkEndOfRoundFinalized();

            // 4) Start next round
            StartNextRound();
        }

        private bool TryEndMatchIfNeeded()
        {
            if (Player1Team!.IsDead || Player2Team!.IsDead)
            {
                Lifecycle.MoveTo(MatchState.Ended);
                // AddEvent(new MatchEnded(Id, _clock.UtcNow));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ends the current player turn.
        /// </summary>
        public Result EndTurn(PlayerId actor)
        {
            if (State != MatchState.Started)
                return Result.Fail(MatchErrors.NotStarted);

            AddEvent(new TurnAdvanced(Id, RoundNumber, _clock.UtcNow));
            return Result.Ok();
        }

        // --------------------
        // Creature context
        // --------------------

        private Result<CreaturePerspective> BuildGameContextForCurrentCreature(CreatureId creatureId)
        {
            if (Player1Team is null || Player2Team is null)
                throw new InvalidOperationException("Teams are not initialized.");

            if (State != MatchState.Started)
                throw new InvalidOperationException("Match is not started.");

            return CreaturePerspective.FromMatch(creatureId, this);
        }
    }
}

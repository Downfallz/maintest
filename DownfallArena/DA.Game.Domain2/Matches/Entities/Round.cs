using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.RuleSets;
using DA.Game.Domain2.Matches.Services;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.Phases;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Utilities;
using System.Collections.ObjectModel;

namespace DA.Game.Domain2.Matches.Entities
{
    public class Round : Entity<RoundId>
    {
        private const string MaxChoicesAlreadySubmittedMessage =
            "Nombre maximum de choix déjà soumis pour ce joueur.";

        // Une seule timeline
        public CombatTimeline Timeline { get; private set; } = CombatTimeline.Empty;

        // Deux curseurs pour deux passes distinctes
        public TurnCursor RevealCursor { get; private set; } = TurnCursor.Start;
        public TurnCursor ResolveCursor { get; private set; } = TurnCursor.Start;

        private readonly Dictionary<CreatureId, CombatActionChoice> _combatChoices = new();
        private readonly Dictionary<CreatureId, CombatActionIntent> _intents = new();

        private readonly HashSet<SpellUnlockChoice> _p1Evolution = new();
        private readonly HashSet<SpellUnlockChoice> _p2Evolution = new();
        private readonly HashSet<SpeedChoice> _p1Speed = new();
        private readonly HashSet<SpeedChoice> _p2Speed = new();

        public int Number { get; private set; }
        public RoundLifecycle Lifecycle { get; } = new();

        public RoundPhase Phase => Lifecycle.Phase;
        public IReadOnlyCollection<SpellUnlockChoice> Player1Choices => _p1Evolution;
        public IReadOnlyCollection<SpellUnlockChoice> Player2Choices => _p2Evolution;

        public IReadOnlyCollection<SpeedChoice> Player1SpeedChoices => _p1Speed;
        public IReadOnlyCollection<SpeedChoice> Player2SpeedChoices => _p2Speed;

        public ReadOnlyDictionary<CreatureId, CombatActionChoice> CombatActionChoices =>
            _combatChoices.AsReadOnly();

        private readonly IGameResources _resources;
        private readonly RuleSet _ruleSet;

        protected Round(RoundId id, IGameResources resources, RuleSet ruleSet) : base(id)
        {
            Number = id.Value;
            _resources = resources;
            _ruleSet = ruleSet;
        }

        public static Round StartFirst(IGameResources resources, RuleSet ruleSet) =>
            new(RoundId.New(1), resources, ruleSet);

        public static Round StartNext(Round previous)
        {
            ArgumentNullException.ThrowIfNull(previous);
            return new Round(RoundId.New(previous.Number + 1), previous._resources, previous._ruleSet);
        }

        // --------------------
        // Phase Évolution
        // --------------------

        public Result InitializeEvolutionPhase()
        {
            return Lifecycle.MoveTo(RoundPhase.Planning);
        }

        public Result SubmitEvolutionChoice(CreaturePerspective ctx, SpellUnlockChoice choice)
        {
            ArgumentNullException.ThrowIfNull(ctx);

            if (Phase != RoundPhase.Planning)
                return Result.Fail("Phase invalide pour soumettre une évolution.");

            if (ctx.Actor.OwnerSlot == PlayerSlot.Player1)
            {
                if (_p1Evolution.Count >= 2)
                    return Result.Fail(MaxChoicesAlreadySubmittedMessage);
                if (!_p1Evolution.Add(choice))
                    return Result.Fail("Choix déjà soumis.");
            }
            else
            {
                if (_p2Evolution.Count >= 2)
                    return Result.Fail(MaxChoicesAlreadySubmittedMessage);
                if (!_p2Evolution.Add(choice))
                    return Result.Fail("Choix déjà soumis.");
            }

            return Result.Ok();
        }

        // --------------------
        // Phase Vitesse
        // --------------------

        public Result InitializeSpeedPhase()
        {
            return Lifecycle.MoveTo(RoundPhase.Planning);
        }

        public Result SubmitSpeedChoice(CreaturePerspective ctx, SpeedChoice choice)
        {
            ArgumentNullException.ThrowIfNull(ctx);

            if (Phase != RoundPhase.Planning)
                return Result.Fail("Phase invalide pour soumettre un choix de vitesse.");

            if (ctx.Actor.OwnerSlot == PlayerSlot.Player1)
            {
                if (_p1Speed.Count >= 3)
                    return Result.Fail(MaxChoicesAlreadySubmittedMessage);
                if (!_p1Speed.Add(choice))
                    return Result.Fail("Choix déjà soumis.");
            }
            else
            {
                if (_p2Speed.Count >= 3)
                    return Result.Fail(MaxChoicesAlreadySubmittedMessage);
                if (!_p2Speed.Add(choice))
                    return Result.Fail("Choix déjà soumis.");
            }

            return Result.Ok();
        }

        // --------------------
        // Phase Combat – init
        // --------------------

        public void InitializeCombatPhase()
        {
            Lifecycle.MoveTo(RoundPhase.Combat);
        }

        /// <summary>
        /// Phase de résolution de vitesse : on fixe la timeline, puis
        /// on reset les deux curseurs (révélation & résolution).
        /// </summary>
        public void InitializeSpeedResolutionPhase(CombatTimeline timeline)
        {
            Timeline = timeline ?? CombatTimeline.Empty;

            RevealCursor = TurnCursor.Start;
            ResolveCursor = TurnCursor.Start;

            IsSpeedResolutionCompleted = true;
            Lifecycle.MoveTo(RoundPhase.Combat);
        }

        // --------------------
        // Combat intents (spell sans target)
        // --------------------

        public Result<CombatActionIntent> SubmitCombatIntent(CreaturePerspective ctx, CombatActionIntent intent)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(intent);

            if (Phase != RoundPhase.Combat)
                return Result<CombatActionIntent>.Fail("Phase invalide pour soumettre un choix d'action de combat.");

            if (_intents.ContainsKey(intent.ActorId))
                return Result<CombatActionIntent>.Fail("Cette créature a déjà soumis une action pour ce round.");

            _intents[intent.ActorId] = intent;

            if (_intents.Count == ctx.Creatures.Count)
            {
                IsCombatActionIntentSubmitPhaseCompleted = true;
            }

            return Result<CombatActionIntent>.Ok(intent);
        }

        // --------------------
        // Combat choices (spell + targets)
        // --------------------

        public Result<CombatActionChoice> SubmitCombatAction(CreaturePerspective ctx, CombatActionChoice choice)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(choice);

            if (Phase != RoundPhase.Combat)
                return Result<CombatActionChoice>.Fail("Phase invalide pour soumettre un choix d'action de combat.");

            if (_combatChoices.ContainsKey(choice.ActorId))
                return Result<CombatActionChoice>.Fail("Cette créature a déjà soumis une action pour ce round.");

            _combatChoices[choice.ActorId] = choice;

            if (_combatChoices.Count == ctx.Creatures.Count)
            {
                IsCombatActionRequestPhaseCompleted = true;
            }

            return Result<CombatActionChoice>.Ok(choice);
        }

        // --------------------
        // Pass 1 : révéler les intents dans l’ordre de la timeline
        // --------------------

        public Result<CombatActionIntent> SelectNextIntentToRevealTarget()
        {
            if (Timeline is null || Timeline.Slots.Count == 0)
                return Result<CombatActionIntent>.Fail("Timeline non initialisé pour ce round.");

            if (RevealCursor.IsEnd(Timeline.Slots.Count))
                return Result<CombatActionIntent>.Fail("Tous les intents ont déjà été révélés pour ce round.");

            var slot = Timeline.Slots[RevealCursor.Index];

            if (!_intents.TryGetValue(slot.CreatureId, out var intent))
                return Result<CombatActionIntent>.Fail("Rien de soumis pour cette créature.");

            // Avance le curseur de révélation
            RevealCursor = RevealCursor.MoveNext();

            return Result<CombatActionIntent>.Ok(intent);
        }

        // --------------------
        // Pass 2 : résoudre les actions (spell + target) dans le même ordre
        // --------------------

        public Result<CombatActionChoice> SelectNextActionToResolve()
        {
            if (Timeline is null || Timeline.Slots.Count == 0)
                return Result<CombatActionChoice>.Fail("Timeline non initialisé pour ce round.");

            if (ResolveCursor.IsEnd(Timeline.Slots.Count))
                return Result<CombatActionChoice>.Fail("Le round est déjà complété.");

            var slot = Timeline.Slots[ResolveCursor.Index];

            if (!_combatChoices.TryGetValue(slot.CreatureId, out var choice))
                return Result<CombatActionChoice>.Fail("Rien de soumis pour cette créature.");

            if (ResolveCursor.IsEnd(Timeline.Slots.Count))
                IsCombatResolutionCompleted = true;

            return Result<CombatActionChoice>.Ok(choice);
        }

        public Result MoveToNextAction()
        {
            // Avance le curseur de résolution
            ResolveCursor = ResolveCursor.MoveNext();
            return Result.Ok();
        }
        // --------------------
        // Fin de round
        // --------------------

        public void DoCleanup()
        {
            Lifecycle.MoveTo(RoundPhase.EndOfRound);
        }

        // --------------------
        // Flags / états dérivés
        // --------------------

        public bool IsEvolutionPhaseComplete =>
            _p1Evolution.Count == 2 && _p2Evolution.Count == 2;

        public bool IsSpeedChoicePhaseComplete =>
            _p1Speed.Count == 3 && _p2Speed.Count == 3;

        public bool IsCombatActionRequestPhaseCompleted { get; private set; }
        public bool IsCombatActionIntentSubmitPhaseCompleted { get; private set; }
        public bool IsSpeedResolutionCompleted { get; private set; }
        public bool IsCombatResolutionCompleted { get; private set; }
    }
}

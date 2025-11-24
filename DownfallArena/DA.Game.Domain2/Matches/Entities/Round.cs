using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.RuleSets;
using DA.Game.Domain2.Matches.Services;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Entities
{
    public class Round : Entity<RoundId>
    {
        private const string MaxChoicesAlreadySubmittedMessage = "Nombre maximum de choix déjà soumis pour ce joueur.";

        public CombatTimeline Timeline { get; private set; } = CombatTimeline.Empty;
        public TurnCursor Cursor { get; private set; } = TurnCursor.Start;
        private readonly Dictionary<CreatureId, CombatActionChoice> _intents = new();

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

        protected Round(RoundId id, IGameResources resources, RuleSet ruleSet) : base(id)
        {
            Number = id.Value;
            _resources = resources;
            _ruleSet = ruleSet;
        }

        private readonly IGameResources _resources;
        private readonly RuleSet _ruleSet;
        public static Round StartFirst(IGameResources resources,
            RuleSet ruleSet) => new Round(RoundId.New(1), resources, ruleSet);

        public static Round StartNext(Round previous)
        {
            ArgumentNullException.ThrowIfNull(previous);
            return new Round(RoundId.New(previous.Number + 1), previous._resources, previous._ruleSet);
        }

        public Result InitializeEvolutionPhase()
        {
            return Lifecycle.MoveTo(RoundPhase.Evolution);
        }

        public Result SubmitEvolutionChoice(PlayerActionContext ctx, SpellUnlockChoice choice)
        {
            ArgumentNullException.ThrowIfNull(ctx);

            if (Phase != RoundPhase.Evolution)
                return Result.Fail("Phase invalide pour soumettre une évolution.");

            if (ctx.Slot == PlayerSlot.Player1)
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

        public Result InitializeSpeedPhase()
        {
            return Lifecycle.MoveTo(RoundPhase.Speed);
        }

        public Result SubmitSpeedChoice(PlayerActionContext ctx, SpeedChoice choice)
        {
            ArgumentNullException.ThrowIfNull(ctx);

            if (Phase != RoundPhase.Speed)
                return Result.Fail("Phase invalide pour soumettre un choix de vitesse.");

            if (ctx.Slot == PlayerSlot.Player1)
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

        public void InitializeCombatPhase()
        {
            Lifecycle.MoveTo(RoundPhase.Combat);
        }

        public Result<CombatActionChoice> SubmitCombatAction(PlayerActionContext ctx, CombatActionChoice choice)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(choice);

            if (Phase != RoundPhase.Combat)
                return Result<CombatActionChoice>.Fail("Phase invalide pour soumettre un choix d'action de combat.");

            if (_intents.ContainsKey(choice.ActorId))
                return Result<CombatActionChoice>.Fail("Cette créature a déjà soumis une action pour ce round.");

            _intents[choice.ActorId] = choice;

            if (_intents.Count == ctx.AllAvailableCharactersCount)
            {
                IsCombatActionRequestPhaseCompleted = true;
            }

            return Result<CombatActionChoice>.Ok(choice);
        }

        public void InitializeSpeedResolutionPhase(CombatTimeline timeline)
        {
            Lifecycle.MoveTo(RoundPhase.SpeedResolution);
            Timeline = timeline;
            IsSpeedResolutionCompleted = true;
            Cursor = TurnCursor.Start;
        }

        public Result<CombatActionChoice> SelectNextActionToResolve()
        {
            if (Timeline is null)
                return Result<CombatActionChoice>.Fail("Timeline non initialisé pour ce round.");

            if (Cursor.IsEnd)
                return Result<CombatActionChoice>.Fail("Le round est déjà complété.");

            var slot = Timeline.Slots[Cursor.Index];

            if (!_intents.TryGetValue(slot.CombatCharacter.Id, out var intent))
            {
                return Result<CombatActionChoice>.Fail("Rien de soumis pour ce character.");
            }
            Cursor = Cursor.MoveNext(Timeline);
            if (Cursor.IsEnd)
            {
                IsCombatResolutionCompleted = true;
            }
            return Result<CombatActionChoice>.Ok(intent);
        }


        public void DoCleanup()
        {
            Lifecycle.MoveTo(RoundPhase.Cleanup);
        }

        public bool IsCombatOver => Cursor.IsEnd || Timeline.AllDead();
        public bool IsEvolutionPhaseComplete => _p1Evolution.Count == 2 && _p2Evolution.Count == 2;

        public bool IsSpeedChoicePhaseComplete => _p1Speed.Count == 3 && _p2Speed.Count == 3;

        public bool IsCombatActionRequestPhaseCompleted { get; private set;  }
        public bool IsSpeedResolutionCompleted { get; private set; }
        public bool IsCombatResolutionCompleted { get; private set; }
    }
}

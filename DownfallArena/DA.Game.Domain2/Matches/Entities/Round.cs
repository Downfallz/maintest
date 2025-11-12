using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Services;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared;

namespace DA.Game.Domain2.Matches.Entities
{
    public class Round : Entity<RoundId>
    {
        public CombatTimeline Timeline { get; private set; } = CombatTimeline.Empty;
        public TurnCursor Cursor { get; private set; } = TurnCursor.Start;
        private readonly Queue<CombatActionIntent> _pendingActions = new();

        private readonly HashSet<SpellUnlockChoice> _p1Evolution = new();
        private readonly HashSet<SpellUnlockChoice> _p2Evolution = new();
        private readonly HashSet<SpeedChoice> _p1Speed = new();
        private readonly HashSet<SpeedChoice> _p2Speed = new();

        public int Number { get; private set; }
        public RoundState State { get; private set; } = RoundState.WaitingForPlayersUnlockChoices;

        public IReadOnlyCollection<SpellUnlockChoice> Player1Choices => _p1Evolution;
        public IReadOnlyCollection<SpellUnlockChoice> Player2Choices => _p2Evolution;

        public IReadOnlyCollection<SpeedChoice> Player1SpeedChoices => _p1Speed;
        public IReadOnlyCollection<SpeedChoice> Player2SpeedChoices => _p2Speed;

        protected Round(RoundId id) : base(id)
        {
            Number = id.Value;
            State = RoundState.WaitingForPlayersUnlockChoices;
        }

        public static Round StartFirst() => new Round(RoundId.New(1));

        public static Round StartNext(Round previous) => new Round(RoundId.New(previous.Number + 1));

        public Result SubmitEvolutionChoice(PlayerActionContext ctx, SpellUnlockChoice choice)
        {
            if (State != RoundState.WaitingForPlayersUnlockChoices)
                return Result.Fail("Phase invalide pour soumettre une évolution.");

            var target = ctx.Slot == PlayerSlot.Player1 ? _p1Evolution : _p2Evolution;

            if (target.Count >= 2)
                return Result.Fail("Nombre maximum de choix déjà soumis pour ce joueur.");

            if (!target.Add(choice))
                return Result.Fail("Choix déjà soumis.");

            if (IsEvolutionPhaseComplete)
                State = RoundState.WaitingForPlayersSpeedChoices;

            return Result.Ok();
        }

        public Result SubmitSpeedChoice(PlayerActionContext ctx, SpeedChoice choice)
        {
            if (State != RoundState.WaitingForPlayersSpeedChoices)
                return Result.Fail("Phase invalide pour soumettre un choix de vitesse.");

            var target = ctx.Slot == PlayerSlot.Player1 ? _p1Speed : _p2Speed;

            if (target.Count >= 3)
                return Result.Fail("Nombre maximum de choix déjà soumis pour ce joueur.");

            if (!target.Add(choice))
                return Result.Fail("Choix déjà soumis.");

            if (IsSpeedChoicePhaseComplete)
                State = RoundState.WaitingForPlayersActions;

            return Result.Ok();
        }
        public void BeginCombatPhase(CombatTimeline timeline)
        {
            Timeline = timeline;
            Cursor = TurnCursor.Start;
            State = RoundState.WaitingForPlayersUnlockChoices;
        }

        public Result<CombatActionIntent> SubmitAction(CombatActionRequest request, RuleSet rules, PlayerActionContext ctx, IClock clock)
        {
            var validator = new ActionValidator(rules, ctx);
            var validation = validator.Validate(request);
            if (!validation.IsSuccess) return Result<CombatActionIntent>.Fail(validation.Error);

            var intent = validation.Value!;
            _pendingActions.Enqueue(intent);

            //AddEvent(new ActionQueued(Id, intent.ActorId, intent.ActionId, clock.UtcNow));
            return Result<CombatActionIntent>.Ok(intent);
        }

        //public Result<CombatActionResult> ResolveNextAction(ActionResolver resolver, RuleSet rules, IClock clock)
        //{
        //    if (!_pendingActions.TryDequeue(out var intent))
        //        return Result<CombatActionResult>.Fail("Aucune action en attente.");

        //    var result = resolver.Resolve(intent, rules, clock);
        //    //AddEvent(new ActionResolved(Id, intent.ActorId, intent.ActionId, result, clock.UtcNow));

        //    Cursor = Cursor.MoveNext(Timeline);
        //    if (Cursor.IsEnd)
        //        //AddEvent(new RoundCombatCompleted(Id, Number, clock.UtcNow));

        //    return Result<CombatActionResult>.Ok(result);
        //}

        public bool IsCombatOver => Cursor.IsEnd || Timeline.AllDead();
        public bool IsEvolutionPhaseComplete => _p1Evolution.Count == 2 && _p2Evolution.Count == 2;

        public bool IsSpeedChoicePhaseComplete => _p1Speed.Count == 3 && _p2Speed.Count == 3;

    }
}

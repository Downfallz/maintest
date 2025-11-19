using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Matches.Resources;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Services;
using DA.Game.Domain2.Shared.Ids;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared;

namespace DA.Game.Domain2.Matches.Entities
{
    public class Round : Entity<RoundId>
    {
        public CombatTimeline Timeline { get; private set; } = CombatTimeline.Empty;
        public TurnCursor Cursor { get; private set; } = TurnCursor.Start;
        private readonly Dictionary<CharacterId, CombatActionChoice> _intents = new();

        private readonly HashSet<SpellUnlockChoice> _p1Evolution = new();
        private readonly HashSet<SpellUnlockChoice> _p2Evolution = new();
        private readonly HashSet<SpeedChoice> _p1Speed = new();
        private readonly HashSet<SpeedChoice> _p2Speed = new();

        public int Number { get; private set; }
        public RoundPhase Phase { get; private set; } = RoundPhase.Evolution;

        public IReadOnlyCollection<SpellUnlockChoice> Player1Choices => _p1Evolution;
        public IReadOnlyCollection<SpellUnlockChoice> Player2Choices => _p2Evolution;

        public IReadOnlyCollection<SpeedChoice> Player1SpeedChoices => _p1Speed;
        public IReadOnlyCollection<SpeedChoice> Player2SpeedChoices => _p2Speed;

        protected Round(RoundId id, IGameResources resources, RuleSet ruleSet) : base(id)
        {
            Number = id.Value;
            Phase = RoundPhase.Evolution;
            _resources = resources;
            _ruleSet = ruleSet;
        }

        private readonly IGameResources _resources;
        private readonly RuleSet _ruleSet;
        public static Round StartFirst(IGameResources resources,
            RuleSet ruleSet) => new Round(RoundId.New(1), resources, ruleSet);

        public static Round StartNext(Round previous) => new Round(RoundId.New(previous.Number + 1), previous._resources, previous._ruleSet);

        public Result<SubmitEvolutionResult> SubmitEvolutionChoice(PlayerActionContext ctx, SpellUnlockChoice choice)
        {
            if (Phase != RoundPhase.Evolution)
                return Result<SubmitEvolutionResult>.Fail("Phase invalide pour soumettre une évolution.");

            var target = ctx.Slot == PlayerSlot.Player1 ? _p1Evolution : _p2Evolution;
            if (ctx.Slot == PlayerSlot.Player1)
            {
                if (_p1Evolution.Count >= 2)
                    return Result<SubmitEvolutionResult>.Fail("Nombre maximum de choix déjà soumis pour ce joueur.");
                if (!_p1Evolution.Add(choice))
                    return Result<SubmitEvolutionResult>.Fail("Choix déjà soumis.");

            }
            else
            {
                if (_p2Evolution.Count >= 2)
                    return Result<SubmitEvolutionResult>.Fail("Nombre maximum de choix déjà soumis pour ce joueur.");
                if (!_p2Evolution.Add(choice))
                    return Result<SubmitEvolutionResult>.Fail("Choix déjà soumis.");
            }

            if (IsEvolutionPhaseComplete)
                Phase = RoundPhase.Speed;

            return Result<SubmitEvolutionResult>.Ok(new SubmitEvolutionResult(target, Phase));
        }

        public Result<SubmitSpeedResult> SubmitSpeedChoice(PlayerActionContext ctx, SpeedChoice choice)
        {
            if (Phase != RoundPhase.Speed)
                return Result<SubmitSpeedResult>.Fail("Phase invalide pour soumettre un choix de vitesse.");

            var target = ctx.Slot == PlayerSlot.Player1 ? _p1Speed : _p2Speed;

            if (ctx.Slot == PlayerSlot.Player1)
            {
                if (_p1Speed.Count >= 3)
                    return Result<SubmitSpeedResult>.Fail("Nombre maximum de choix déjà soumis pour ce joueur.");
                if (!_p1Speed.Add(choice))
                    return Result<SubmitSpeedResult>.Fail("Choix déjà soumis.");
            }
            else
            {
                if (_p2Speed.Count >= 3)
                    return Result<SubmitSpeedResult>.Fail("Nombre maximum de choix déjà soumis pour ce joueur.");
                if (!_p2Speed.Add(choice))
                    return Result<SubmitSpeedResult>.Fail("Choix déjà soumis.");
            }

            if (IsSpeedChoicePhaseComplete)
                Phase = RoundPhase.Combat;

            return Result<SubmitSpeedResult>.Ok(new SubmitSpeedResult(target, Phase));
        }

        public void BeginCombatPhase(CombatTimeline timeline)
        {
            Timeline = timeline;
            Cursor = TurnCursor.Start;
        }

        public Result<CombatActionChoice> SubmitCombatAction(PlayerActionContext ctx, CombatActionChoice choice)
        {
            if (Phase != RoundPhase.Combat)
                return Result<CombatActionChoice>.Fail("Phase invalide pour soumettre un choix d'action de combat.");

            var validator = new ActionValidator(_ruleSet);
            var resultIntent = validator.Validate(ctx, choice);
            if (!resultIntent.IsSuccess)
                return Result<CombatActionChoice>.Fail(resultIntent.Error!);

            if (_intents.ContainsKey(choice.ActorId))
                return Result<CombatActionChoice>.Fail("Cette créature a déjà soumis une action pour ce round.");

            _intents[choice.ActorId] = choice;

            if (_intents.Count == ctx.AllAvailableCharactersCount)
                Phase = RoundPhase.CombatResolution;

            //AddEvent(new ActionQueued(Id, intent.ActorId, intent.ActionId, clock.UtcNow));
            return Result<CombatActionChoice>.Ok(choice);
        }
        public Result<CombatActionResult> ResolveNextAction()
        {
            if (Timeline is null)
                return Result<CombatActionResult>.Fail("Timeline non initialisé pour ce round.");

            if (Cursor.IsEnd)
                return Result<CombatActionResult>.Fail("Le round est déjà complété.");

            var slot = Timeline.Slots[Cursor.Index];

            if (!_intents.TryGetValue(slot.CombatCharacter.Id, out var intent))
            {
                return Result<CombatActionResult>.Fail("Rien de soumis pour ce character.");
            }
            
            //var result = resolver.Resolve(intent, rules, clock);
            //AddEvent(new ActionResolved(Id, intent.ActorId, intent.ActionId, result, clock.UtcNow));

            Cursor = Cursor.MoveNext(Timeline);

            if (Cursor.IsEnd)
            {
                Phase = RoundPhase.Completed;
                //AddEvent(new RoundCombatCompleted(Id, Number, clock.UtcNow));
            }

            return Result<CombatActionResult>.Ok(new CombatActionResult(intent, new List<EffectSummary>()));
        }

        public bool IsCombatOver => Cursor.IsEnd || Timeline.AllDead();
        public bool IsEvolutionPhaseComplete => _p1Evolution.Count == 2 && _p2Evolution.Count == 2;

        public bool IsSpeedChoicePhaseComplete => _p1Speed.Count == 3 && _p2Speed.Count == 3;

    }
}

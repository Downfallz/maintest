using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Events;
using DA.Game.Domain2.Matches.Events.Combat;
using DA.Game.Domain2.Matches.Events.Evolution;
using DA.Game.Domain2.Matches.Events.Match;
using DA.Game.Domain2.Matches.Events.Round;
using DA.Game.Domain2.Matches.Events.Speed;
using DA.Game.Domain2.Matches.Messages;
using DA.Game.Domain2.Matches.RuleSets;
using DA.Game.Domain2.Matches.Services;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.MatchVo;
using DA.Game.Domain2.Matches.ValueObjects.SpeedVo;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Players;
using DA.Game.Shared.Contracts.Players.Ids;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Utilities;
using System.Collections.ObjectModel;
using System.Data.SqlTypes;

namespace DA.Game.Domain2.Matches.Aggregates;

/// <summary>
/// Représente un match entre deux joueurs.
/// Contient la logique principale du cycle de vie : join, rounds, évolutions, tours.
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
        _ruleSet = ruleSet;
        _clock = clock;
    }

    /// <summary>
    /// Crée un nouveau match avec les ressources de jeu fournies.
    /// </summary>
    public static Match Create(IGameResources resources, RuleSet ruleSet, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return new Match(MatchId.New(), resources, ruleSet, clock);
    }

    /// <summary>
    /// Permet à un joueur de rejoindre le match. Le match démarre automatiquement si les deux sont présents.
    /// </summary>
    public Result<PlayerSlot> Join(PlayerRef player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (State != MatchState.WaitingForPlayers)
            return Result<PlayerSlot>.Fail(MatchErrors.AlreadyStarted);

        if (PlayerRef1?.Id == player.Id || PlayerRef2?.Id == player.Id)
            return Result<PlayerSlot>.Fail(MatchErrors.PlayerAlreadyInGame);
        PlayerSlot slot = default;
        if (PlayerRef1 is null)
        {
            PlayerRef1 = player;
            AddEvent(new PlayerJoined(Id, player.Id, _clock.UtcNow));
            slot = PlayerSlot.Player1;
        }
        else if (PlayerRef2 is null)
        {
            PlayerRef2 = player;
            AddEvent(new PlayerJoined(Id, player.Id, _clock.UtcNow));
            slot = PlayerSlot.Player2;
        }

        if (PlayerRef1 is not null && PlayerRef2 is not null)
            AddEvent(new AllPlayersReady(Id, _clock.UtcNow));

        return Result<PlayerSlot>.Ok(slot);
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
        Player1Team = Team.FromCharacterTemplateAndSlot(_resources.GetCharacter(CreatureDefId.New("creature:main:v1")), PlayerSlot.Player1);
        Player2Team = Team.FromCharacterTemplateAndSlot(_resources.GetCharacter(CreatureDefId.New("creature:main:v1")), PlayerSlot.Player2);
    }

    private void InitializeFirstRound()
    {

        CurrentRound = Round.StartFirst(_resources, _ruleSet);
        InitializeRound();
    }

    public void InitializeNextRound()
    {
        CurrentRound = Round.StartNext(CurrentRound!);
        InitializeRound();
    }

    private void InitializeRound()
    {

        //// global +2 Energy
        //foreach (var creature in AllCreatures)
        //    creature.GainEnergy(2);

        //foreach (var creature in AllCreatures)
        //    creature.ResolveConditionsAtRoundStart();


        AddEvent(new RoundInitialized(Id, CurrentRound!.Id, RoundNumber, DateTime.UtcNow));
        InitializeCurrentRoundEvolution();
    }
    private void InitializeCurrentRoundEvolution()
    {
        CurrentRound!.InitializeEvolutionPhase();
    }

    /// <summary>
    /// Soumet le choix d’évolution (déblocage de sort) du joueur.
    /// </summary>
    public Result SubmitEvolutionChoice(PlayerSlot player, SpellUnlockChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var guard = _ruleSet.Phase.MatchPhase.EnsureCanSubmitEvolutionChoice(this);
        if (!guard.IsSuccess)
            return guard;

        var ctxResult = BuildGameContextForCurrentCreature(choice.CharacterId);
        if (!ctxResult.IsSuccess)
            return Result.Fail(ctxResult.Error!);
        var result = CurrentRound!.SubmitEvolutionChoice(ctxResult.Value!, choice);

        if (!result.IsSuccess)
            return result;

        AddEvent(new EvolutionChoiceSubmitted(CurrentRound.Id, ctxResult.Value!.Actor.OwnerSlot, choice, _clock.UtcNow));

        if (CurrentRound.IsEvolutionPhaseComplete)
            AddEvent(new EvolutionPhaseCompleted(Id, CurrentRound.Id, RoundNumber, _clock.UtcNow));

        return result;
    }

    public Result InitializeCurrentRoundSpeedPhase()
    {
        return CurrentRound!.InitializeSpeedPhase();
    }

    /// <summary>
    /// Soumet le choix de vitesse (Quick/Standard) du joueur.
    /// </summary>
    public Result SubmitSpeedChoice(PlayerSlot slot, SpeedChoice speedChoice)
    {
        ArgumentNullException.ThrowIfNull(speedChoice);

        var guard = _ruleSet.Phase.MatchPhase.EnsureCanSubmitSpeedChoice(this);
        if (!guard.IsSuccess)
            return guard;

        var ctxResult = BuildGameContextForCurrentCreature(speedChoice.CharacterId);
        if (!ctxResult.IsSuccess)
            return Result.Fail(ctxResult.Error!);
        var result = CurrentRound!.SubmitSpeedChoice(ctxResult.Value!, speedChoice);

        if (!result.IsSuccess)
            return result;

        AddEvent(new SpeedChoiceSubmitted(CurrentRound.Id, slot, speedChoice, _clock.UtcNow));

        if (CurrentRound.IsSpeedChoicePhaseComplete)
            AddEvent(new SpeedPhaseCompleted(Id, CurrentRound.Id, RoundNumber, _clock.UtcNow));

        return result;
    }

    public Result InitializeCurrentRoundCombatPhase()
    {
        CurrentRound!.InitializeCombatPhase();
        return Result.Ok();
    }

    public Result SubmitCombatAction(CombatActionChoice combatActionChoice)
    {
        ArgumentNullException.ThrowIfNull(combatActionChoice);

        var guard = _ruleSet.Phase.MatchPhase.EnsureCanSubmitCombatAction(this);
        if (!guard.IsSuccess)
            return guard;

        var ctxResult = BuildGameContextForCurrentCreature(combatActionChoice.ActorId);
        if (!ctxResult.IsSuccess)
            return Result.Fail(ctxResult.Error!);

        var validateResult = _ruleSet.Combat.ValidateAction(ctxResult.Value!, combatActionChoice);
        if (!validateResult.IsSuccess)
        {
            return validateResult;
        }
        var result = CurrentRound!.SubmitCombatAction(ctxResult.Value!, combatActionChoice);
        if (!result.IsSuccess)
            return Result.Fail(result.Error!);

        AddEvent(new CombatActionChoiceSubmitted(CurrentRound!.Id, combatActionChoice, _clock.UtcNow));

        if (CurrentRound!.IsCombatActionRequestPhaseCompleted)
        {
            AddEvent(new CombatActionRequestPhaseCompleted(Id, CurrentRound!.Id, RoundNumber, _clock.UtcNow));
            InitializeCurrentRoundSpeedResolutionPhase();
        }

        return Result.Ok();
    }

    private void InitializeCurrentRoundSpeedResolutionPhase()
    {
        var timeline = CombatTimeline.FromSpeedChoices(
            Player1Team!,
            CurrentRound!.Player1SpeedChoices,
            Player2Team!,
            CurrentRound.Player2SpeedChoices,
            _ruleSet);

        CurrentRound!.InitializeSpeedResolutionPhase(timeline);
        AddEvent(new SpeedResolutionCompleted(Id, CurrentRound!.Id, RoundNumber, _clock.UtcNow));
    }

    public Result<bool> ResolveNextCombatStep()
    {
        if (CurrentRound is null)
            return Result<bool>.Fail("Aucun round actif.");

        var next = CurrentRound.SelectNextActionToResolve();
        if (!next.IsSuccess)
            return Result<bool>.Fail(next.Error!);

        var intent = next.Value!;

        var ctxResult = BuildGameContextForCurrentCreature(intent.ActorId);
        if (!ctxResult.IsSuccess)
            return Result<bool>.Fail(ctxResult.Error!);

        //// 1) Résoudre les effets pour cette action
        var combatactionresult = _ruleSet.Combat.Resolve(ctxResult.Value!, intent);

        //// 2) Appliquer les effets sur les teams / créatures
        var appli = _ruleSet.Combat.ApplyCombatResult(combatactionresult.Value!, AllCreatures);

        //// 3) Lever un event d’action résolue
        //AddEvent(new CombatActionResolved(
        //    MatchId: Id,
        //    RoundId: CurrentRound.Id,
        //    RoundNumber: RoundNumber,
        //    Intent: intent,
        //    Effects: effects,
        //    ResolvedAt: _clock.UtcNow));

        // 4) Si tout est terminé → fin de résolution + cleanup
        if (CurrentRound.IsCombatResolutionCompleted)
        {
            AddEvent(new CombatResolutionCompleted(
                Id, CurrentRound.Id, RoundNumber, _clock.UtcNow));

            InitializeCurrentRoundCleanup();
            return Result<bool>.Ok(true);
        }

        return Result<bool>.Ok(false);
    }

    private void InitializeCurrentRoundCleanup()
    {
        if (Player1Team!.IsDead || Player2Team!.IsDead)
        {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            Console.WriteLine("the end");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
        }
        CurrentRound!.DoCleanup();
        AddEvent(new RoundEnded(Id, CurrentRound.Id, RoundNumber, _clock.UtcNow));
        InitializeNextRound();
    }

    /// <summary>
    /// Termine le tour courant du joueur.
    /// </summary>
    public Result EndTurn(PlayerId actor)
    {
        if (State != MatchState.Started)
            return Result.Fail(MatchErrors.NotStarted);

        AddEvent(new TurnAdvanced(Id, RoundNumber, _clock.UtcNow));
        return Result.Ok();
    }


    private Result<GameContext> BuildGameContextForCurrentCreature(CreatureId creatureId)
    {
        if (Player1Team is null || Player2Team is null)
            throw new InvalidOperationException("Teams are not initialized.");

        if (State != MatchState.Started)
            throw new InvalidOperationException("Match is not started.");

        return GameContext.FromMatch(creatureId, this);
    }
}

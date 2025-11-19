using DA.Game.Domain2.Catalog.Ids;
using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Match.Events;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Matches.Messages;
using DA.Game.Domain2.Matches.Resources;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Players.Ids;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared;

namespace DA.Game.Domain2.Matches.Aggregates;

/// <summary>
/// Représente un match entre deux joueurs.
/// Contient la logique principale du cycle de vie : join, rounds, évolutions, tours.
/// </summary>
public sealed class Match : AggregateRoot<MatchId>
{
    private readonly IGameResources _resources;
    private readonly RuleSet _ruleSet;
    public MatchLifecycle Lifecycle { get; } = new();

    public MatchState State => Lifecycle.State;

    public PlayerRef? PlayerRef1 { get; private set; }
    public PlayerRef? PlayerRef2 { get; private set; }
    public Team? Player1Team { get; private set; }
    public Team? Player2Team { get; private set; }
    public Round? CurrentRound { get; private set; }
    public int RoundNumber => CurrentRound?.Number ?? 0;

    private Match(MatchId id, IGameResources resources, RuleSet ruleSet) : base(id)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _ruleSet = ruleSet;
    }

    /// <summary>
    /// Crée un nouveau match avec les ressources de jeu fournies.
    /// </summary>
    public static Match Create(IGameResources resources, RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return new Match(MatchId.New(), resources, ruleSet);
    }

    /// <summary>
    /// Permet à un joueur de rejoindre le match. Le match démarre automatiquement si les deux sont présents.
    /// </summary>
    public Result Join(PlayerRef player, IClock clock, IRandom rng)
    {
        if (State != MatchState.WaitingForPlayers)
            return Result.Fail(MatchErrors.AlreadyStarted);

        if (PlayerRef1?.Id == player.Id || PlayerRef2?.Id == player.Id)
            return Result.Fail(MatchErrors.PlayerAlreadyInGame);
        PlayerSlot slot = default;
        if (PlayerRef1 is null)
        {
            PlayerRef1 = player;
            AddEvent(new PlayerJoined(Id, player.Id, clock.UtcNow));
            slot = PlayerSlot.Player1;
        }
        else if (PlayerRef2 is null)
        {
            PlayerRef2 = player;
            AddEvent(new PlayerJoined(Id, player.Id, clock.UtcNow));
            slot = PlayerSlot.Player2;
        }

        if (PlayerRef1 is not null && PlayerRef2 is not null)
            StartMatch(clock, rng);

        return Result.Ok();
    }

    private void StartMatch(IClock clock, IRandom rng)
    {
        InitializeTeams();
        InitializeFirstRound();
        Lifecycle.MoveTo(MatchState.Started);
        AddEvent(new MatchStarted(Id, clock.UtcNow));
    }

    private void InitializeTeams()
    {
        Player1Team = Team.FromCharacterTemplate(_resources.GetCharacter(CharacterDefId.New("creature:main:v1")));
        Player2Team = Team.FromCharacterTemplate(_resources.GetCharacter(CharacterDefId.New("creature:main:v1")));
    }

    private void InitializeFirstRound()
    {

        CurrentRound = Round.StartFirst(_resources, _ruleSet);
        AddEvent(new TurnAdvanced(Id, RoundNumber, DateTime.UtcNow));
    }

    private void InitializeNextRound()
    {

        CurrentRound = Round.StartNext(CurrentRound!);
        AddEvent(new TurnAdvanced(Id, RoundNumber, DateTime.UtcNow));
    }
    /// <summary>
    /// Soumet le choix d’évolution (déblocage de sort) du joueur.
    /// </summary>
    public Result SubmitEvolutionChoice(PlayerSlot player, SpellUnlockChoice choice, IClock clock)
    {
        var guard = _ruleSet.Phase.MatchPhase.EnsureCanSubmitEvolutionChoice(this);
        if (!guard.IsSuccess)
            return guard;

        var playerCtx = BuildContextForCurrentPlayer(player);
        var result = CurrentRound!.SubmitEvolutionChoice(playerCtx, choice);

        if (!result.IsSuccess)
            return result;

        AddEvent(new EvolutionChoiceSubmitted(CurrentRound.Id, playerCtx.Slot, choice, clock.UtcNow));

        if (CurrentRound.IsEvolutionPhaseComplete)
            AddEvent(new EvolutionPhaseCompleted(CurrentRound.Id, RoundNumber, clock.UtcNow));

        return result;
    }

    /// <summary>
    /// Soumet le choix de vitesse (Quick/Standard) du joueur.
    /// </summary>
    public Result SubmitSpeedChoice(PlayerSlot slot, SpeedChoice speedChoice, IClock clock)
    {
        var guard = _ruleSet.Phase.MatchPhase.EnsureCanSubmitSpeedChoice(this);
        if (!guard.IsSuccess)
            return guard;

        var playerCtx = BuildContextForCurrentPlayer(slot);
        var result = CurrentRound!.SubmitSpeedChoice(playerCtx, speedChoice);

        if (!result.IsSuccess)
            return result;

        AddEvent(new SpeedChoiceSubmitted(CurrentRound.Id, slot, speedChoice, clock.UtcNow));

        if (CurrentRound.IsSpeedChoicePhaseComplete)
            AddEvent(new SpeedPhaseCompleted(CurrentRound.Id, RoundNumber, clock.UtcNow));

        return result;
    }

    public Result SubmitCombatAction(PlayerSlot slot, CombatActionChoice combatActionChoice, IClock clock)
    {
        var guard = _ruleSet.Phase.MatchPhase.EnsureCanSubmitCombatAction(this);
        if (!guard.IsSuccess)
            return guard;

        var playerCtx = BuildContextForCurrentPlayer(slot);
        var result = CurrentRound!.SubmitCombatAction(playerCtx, combatActionChoice);

        AddEvent(new CombatActionChoiceSubmitted(CurrentRound!.Id, slot, combatActionChoice, clock.UtcNow));

        if (CurrentRound!.Phase == RoundPhase.CombatResolution)
        {
            AddEvent(new CombatActionRequestPhaseCompleted(CurrentRound!.Id, RoundNumber, clock.UtcNow));

            var timeline = CombatTimeline.FromSpeedChoices(
            Player1Team,
            CurrentRound.Player1SpeedChoices,
            Player2Team,
            CurrentRound.Player1SpeedChoices,
            _ruleSet);

            CurrentRound!.BeginCombatPhase(timeline);

            while (CurrentRound!.Phase != RoundPhase.Completed)
            {
                var actionResult = CurrentRound!.ResolveNextAction();
                if (!actionResult.IsSuccess)
                    return Result.Fail(actionResult.Error!);
                AddEvent(new CombatActionResolved(CurrentRound!.Id, actionResult.Value!, clock.UtcNow));
            }

            AddEvent(new CombatResolvingPhaseCompleted(CurrentRound!.Id, RoundNumber, clock.UtcNow));
            InitializeNextRound();
        }

        return Result.Ok();
    }

    /// <summary>
    /// Termine le tour courant du joueur.
    /// </summary>
    public Result EndTurn(PlayerId actor, IClock clock)
    {
        if (State != MatchState.Started)
            return Result.Fail(MatchErrors.NotStarted);

        AddEvent(new TurnAdvanced(Id, RoundNumber, clock.UtcNow));
        return Result.Ok();
    }

    private PlayerActionContext BuildContextForCurrentPlayer(PlayerSlot slot)
    {
        if (Player1Team is null || Player2Team is null)
            throw new InvalidOperationException("Teams are not initialized.");

        if (State != MatchState.Started)
            throw new InvalidOperationException("Match is not started.");

        var my = slot == PlayerSlot.Player1 ? Player1Team : Player2Team;
        var enemy = slot == PlayerSlot.Player1 ? Player2Team : Player1Team;
        return PlayerActionContext.FromTeams(slot, my, enemy);
    }
}

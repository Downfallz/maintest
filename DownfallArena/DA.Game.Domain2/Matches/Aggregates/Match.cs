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
    private readonly GameResources _resources;

    public MatchState State { get; private set; } = MatchState.WaitingForPlayers;
    public PlayerRef? PlayerRef1 { get; private set; }
    public PlayerRef? PlayerRef2 { get; private set; }
    public Team? Player1Team { get; private set; }
    public Team? Player2Team { get; private set; }
    public Round? CurrentRound { get; private set; }
    public int RoundNumber => CurrentRound?.Number ?? 0;

    private Match(MatchId id, GameResources resources) : base(id)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    /// <summary>
    /// Crée un nouveau match avec les ressources de jeu fournies.
    /// </summary>
    public static Match Create(GameResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return new Match(MatchId.New(), resources);
    }

    /// <summary>
    /// Permet à un joueur de rejoindre le match. Le match démarre automatiquement si les deux sont présents.
    /// </summary>
    public Result<MatchState> Join(PlayerRef player, IClock clock, IRandom rng)
    {
        if (State != MatchState.WaitingForPlayers)
            return Result<MatchState>.Fail(MatchErrors.AlreadyStarted);

        if (PlayerRef1?.Id == player.Id || PlayerRef2?.Id == player.Id)
            return Result<MatchState>.Fail(MatchErrors.PlayerAlreadyInGame);

        if (PlayerRef1 is null)
        {
            PlayerRef1 = player;
            AddEvent(new PlayerJoined(Id, player.Id, clock.UtcNow));
        }
        else if (PlayerRef2 is null)
        {
            PlayerRef2 = player;
            AddEvent(new PlayerJoined(Id, player.Id, clock.UtcNow));
        }

        if (PlayerRef1 is not null && PlayerRef2 is not null)
            StartMatch(clock, rng);

        return Result<MatchState>.Ok(State);
    }

    private void StartMatch(IClock clock, IRandom rng)
    {
        InitializeTeams();
        InitializeFirstRound();
        State = MatchState.Started;
        AddEvent(new MatchStarted(Id, clock.UtcNow));
    }

    private void InitializeTeams()
    {
        // TODO: Remplacer CharacterDefId.New() par une vraie sélection
        Player1Team = Team.FromCharacterTemplate(_resources.GetCharacter(CharacterDefId.New()));
        Player2Team = Team.FromCharacterTemplate(_resources.GetCharacter(CharacterDefId.New()));
    }

    private void InitializeFirstRound()
    {
        CurrentRound = Round.StartFirst();
        AddEvent(new TurnAdvanced(Id, RoundNumber, DateTime.UtcNow));
    }

    /// <summary>
    /// Soumet le choix d’évolution (déblocage de sort) du joueur.
    /// </summary>
    public Result SubmitEvolutionChoice(PlayerSlot slot, SpellUnlockChoice choice, IClock clock)
    {
        var playerCtx = BuildContextForCurrentPlayer(slot);
        var result = CurrentRound?.SubmitEvolutionChoice(playerCtx, choice);

        if (result is null)
            return Result.Fail("No current round.");

        if (!result.Value.IsSuccess)
            return Result.Fail(result.Value.Error);

        AddEvent(new EvolutionChoiceSubmitted(CurrentRound!.Id, slot, choice, clock.UtcNow));

        if (CurrentRound!.IsEvolutionPhaseComplete)
            AddEvent(new EvolutionPhaseCompleted(CurrentRound!.Id, RoundNumber, clock.UtcNow));

        return Result.Ok();
    }

    /// <summary>
    /// Soumet le choix de vitesse (Quick/Standard) du joueur.
    /// </summary>
    public Result SubmitSpeedChoice(PlayerSlot slot, SpeedChoice speedChoice, IClock clock)
    {
        var playerCtx = BuildContextForCurrentPlayer(slot);
        var result = CurrentRound?.SubmitSpeedChoice(playerCtx, speedChoice);

        if (result is null)
            return Result.Fail("No current round.");

        if (!result.Value.IsSuccess)
            return Result.Fail(result.Value.Error);

        AddEvent(new SpeedChoiceSubmitted(CurrentRound!.Id, slot, speedChoice, clock.UtcNow));

        if (CurrentRound!.IsEvolutionPhaseComplete)
            AddEvent(new SpeedPhaseCompleted(CurrentRound!.Id, RoundNumber, clock.UtcNow));

        return Result.Ok();
    }
    //public Result<CombatActionIntent> SubmitCombatAction(CombatActionRequest request, RuleSet rules, PlayerActionContext ctx, IClock clock)
    //{
    //    var a = CurrentRound?.SubmitAction(request, rules, ctx, clock);
        
    //}

    public Result SubmitCombatAction(PlayerSlot slot, SpeedChoice speedChoice, IClock clock)
    {
        var playerCtx = BuildContextForCurrentPlayer(slot);
        var result = CurrentRound?.SubmitSpeedChoice(playerCtx, speedChoice);

        if (result is null)
            return Result.Fail("No current round.");

        if (!result.Value.IsSuccess)
            return Result.Fail(result.Value.Error);

        AddEvent(new SpeedChoiceSubmitted(CurrentRound!.Id, slot, speedChoice, clock.UtcNow));

        if (CurrentRound!.IsEvolutionPhaseComplete)
            AddEvent(new SpeedPhaseCompleted(CurrentRound!.Id, RoundNumber, clock.UtcNow));

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

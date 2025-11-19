using AutoMapper;
using DA.Game.Application.Learning.Abstractions;
using DA.Game.Application.Matches.Features.JoinMatch;
using DA.Game.Application.Matches.Features.PlayTurn;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.Simulation.Results;
using DA.Game.Application.Matches.Simulation.Scenarios;
using DA.Game.Application.Players.Ports;
using DA.Game.Domain2.Match.Entities;
using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Match.Events;
using DA.Game.Domain2.Match.ReadModels;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Matches.Resources;
using DA.Game.Domain2.Players.Ids;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Matches.Simulation.Runners;

// Application/Matches/Simulation/Runners/FidelitySimulationRunner.cs
public interface ISimulationRunner
{
    Task<MatchResult> RunAsync(MatchScenario scenario, CancellationToken ct = default);
}

public sealed class FidelitySimulationRunner(
    IMediator mediator,
    IMatchRepository matches,
    IPlayerRepository players,
    ITurnDeciderRegistry deciders,
    IClock clock,
    IMapper mapper,
    IDatasetLogger? dataset,// nullable: actif en mode simulation/ML
    IGameResources gameResources,
    IRuleSetProvider ruleSetProvider
) : ISimulationRunner
{
    public async Task<MatchResult> RunAsync(MatchScenario scenario, CancellationToken ct = default)
    {
        // 1) Setup players (création/seed côté repo ou via use case)
        var p1 = await EnsurePlayerAsync(scenario.Player1, ct);
        var p2 = await EnsurePlayerAsync(scenario.Player2, ct);

        // 2) Crée un match et join
        
        var match = Match.Create(gameResources, ruleSetProvider.Current);
        var matchId = match.Id;
        var matchResult = await matches.SaveAsync(match, ct);
        match = matchResult.Value;

        var pr1 = mapper.Map<PlayerRef>(p1);
        var pr2 = mapper.Map<PlayerRef>(p2);
        await mediator.Send(new JoinMatchCommand(matchId, pr1), ct);
        await mediator.Send(new JoinMatchCommand(matchId, pr2), ct);

        // Résolution du decider
        var decider = new RandomBotDecider(new SystemRandom());

        // 3) Boucle de tours
        var turns = 0;
        for (; turns < scenario.MaxTurns; turns++)
        {
            var m = await matches.GetAsync(matchId, ct);
            if (m is null || m.State != MatchState.Started) break;

            var currentRef = PlayerSlot.Player1 == PlayerSlot.Player1 ? m.PlayerRef1! : m.PlayerRef2!;
            var view = new GameView(m.Id, PlayerSlot.Player1, m.CurrentRound?.Number ?? 0, m.PlayerRef1?.Id, m.PlayerRef2?.Id);

            var action = await decider.DecideAsync(currentRef.Id, view, ct)
                         ?? new PlayerAction("noop", "sim-default"); // fallback pour humains en sim
            var reward = new Random().Next(-2, 5);
            dataset?.Record(view, action, reward); // <= log pour ML

            await mediator.Send(new PlayTurnCommand(matchId, currentRef.Id, action), ct);
        }

        // 4) Résumé
        var final = await matches.GetAsync(matchId, ct);
        return new MatchResult(
            MatchId: matchId,
            Scenario: scenario.Name,
            TurnsPlayed: turns,
            FinalState: final?.State ?? MatchState.WaitingForPlayers,
            LastTurnBy: PlayerSlot.Player1, // fix avec le timeline si besoin
            Winner: null // à compléter quand tu auras une condition de victoire
        );
    }

    private async Task<Player> EnsurePlayerAsync(PlayerSimProfile profile, CancellationToken ct)
    {
        // Ici je seed via repo directement. 
        // Variante: envoie CreatePlayerCommand si tu veux passer par le use case.
        //var existing = await players.FindByNameAsync?.Invoke(profile.Name, ct) ?? null;
        //if (existing is not null) return existing;

        var p = new Player(PlayerId.New(), profile.Name, profile.Kind);
        await players.SaveAsync(p, ct);
        return p;
    }
}

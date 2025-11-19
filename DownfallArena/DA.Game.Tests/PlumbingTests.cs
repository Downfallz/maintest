using AutoMapper;
using DA.Game.Application.Learning.Abstractions;
using DA.Game.Application.Learning.ML;
using DA.Game.Application.Matches.Features.CreateMatch;
using DA.Game.Application.Matches.Features.JoinMatch;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.Simulation.Runners;
using DA.Game.Application.Matches.Simulation.Scenarios;
using DA.Game.Application.Players.Features.Create;
using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Match.ReadModels;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Players.Enums;
using DA.Game.Domain2.Players.Ids;
using DA.Game.Tests.Support;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace DA.Game.Tests;

public sealed class PlumbingTests
{
    private readonly TestFixture _fx = new();

    [Fact]
    public async Task When_two_players_join_match_starts_and_turn_advances()
    {
        // Arrange
        var cmdBus = _fx.Get<IMediator>();

        var match = await cmdBus.Send(new CreateMatchCommand());
        if (match.IsSuccess == false)
        {
            throw new InvalidOperationException("Failed to create match: " + match.Error);
        }

        // Act
        var r1 = await cmdBus.Send(new CreatePlayerCommand("Alice", ActorKind.Human));
        var r2 = await cmdBus.Send(new CreatePlayerCommand("BOT-42", ActorKind.Bot));
        if (r1.IsSuccess == false)
        {
            throw new InvalidOperationException("Failed to create player 1: " + r1.Error);
        }
        if (r2.IsSuccess == false)
        {
            throw new InvalidOperationException("Failed to create player 2: " + r2.Error);
        }
        var map = _fx.Get<IMapper>();

        var pr1 = map.Map<PlayerRef>(r1.Value!);
        var pr2 = map.Map<PlayerRef>(r2.Value!);
        var matchId = match.Value!.Id;
        await cmdBus.Send(new JoinMatchCommand(matchId, pr1));
        await cmdBus.Send(new JoinMatchCommand(matchId, pr2));

        // Assert
        var repo = _fx.Get<IMatchRepository>();
        var match2 = await repo.GetAsync(matchId, CancellationToken.None);
        match2.Should().NotBeNull();
        match2.Should().NotBeNull();
        match2.State.Should().Be(MatchState.Started);
        //match2.CurrentPlayerSlot.Should().NotBeNull();
        match2.RoundNumber.Should().Be(1);
    }

    [Fact]
    public async Task Bot_vs_Bot_should_progress_multiple_turns()
    {
        var sim = _fx.Get<ISimulationRunner>();

        var scn = new MatchScenario(
            "SmokeTest",
            new PlayerSimProfile("BOT-A", ActorKind.Bot),
            new PlayerSimProfile("BOT-B", ActorKind.Bot),
            MaxTurns: 30);

        var res = await sim.RunAsync(scn);
        var dslogger = _fx.Get<IDatasetLogger>();
        //dslogger.SaveCsv("C:\\gamedata\\test.csv", true);
        res.TurnsPlayed.Should().BeGreaterThan(1);

        var trainer = _fx.Get<ITrainer>();
        trainer.Train("C:\\gamedata\\test.csv", "C:\\gamedata\\model.zip");

    }


    [Fact]
    public async Task test_ml()
    {
        var sim = _fx.Get<ISimulationRunner>();

        var scn = new MatchScenario(
            "SmokeTest",
            new PlayerSimProfile("BOT-A", ActorKind.Bot),
            new PlayerSimProfile("BOT-B", ActorKind.Bot),
            MaxTurns: 8);

        var res = await sim.RunAsync(scn);
        var dslogger = _fx.Get<IDatasetLogger>();
        dslogger.SaveCsv("C:\\gamedata\\test2.csv", true);
        res.TurnsPlayed.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task test_train()
    {
        var trainer = new RewardTrainer();
        trainer.Train("C:\\gamedata\\test2.csv", "C:\\gamedata\\model-reward.zip");
    }

    [Fact]
    public async Task test_predict()
    {
        var predictor = new RewardPredictor("C:\\gamedata\\model-reward.zip", _fx.Get<IFeatureExtractor>());

        var gvTest = new GameView(MatchId.New(), PlayerSlot.Player1, 5, PlayerId.New(), PlayerId.New());

        var predFireball = predictor.PredictReward(gvTest, "Fireball");
        var predHeal = predictor.PredictReward(gvTest, "Heal");

        Console.WriteLine($"Pred(Fireball)={predFireball:F2}, Pred(Heal)={predHeal:F2}");
    }

}

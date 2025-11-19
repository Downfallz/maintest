using AutoMapper;
using DA.Game.Application.Learning.Abstractions;
using DA.Game.Application.Learning.ML;
using DA.Game.Application.Matches.Features.CreateMatch;
using DA.Game.Application.Matches.Features.JoinMatch;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.Simulation.Runners;
using DA.Game.Application.Matches.Simulation.Scenarios;
using DA.Game.Application.Players.Features.Create;
using DA.Game.DataBuilder;
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

namespace DA.Game.Tests.Infrastructure.Bootstrap;

public sealed class DataBuilderTests
{
    private readonly TestFixture _fx = new();

    [Fact]
    public async Task When_two_players_join_match_starts_and_turn_advances()
    {
      await GameSchemaBuilder.BuildAsync();
    }

    
}

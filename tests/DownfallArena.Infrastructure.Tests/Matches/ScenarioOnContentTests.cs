using DownfallArena.Application;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Randomness;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Infrastructure.Tests.Matches;

/// <summary>
/// A match scenario on the repository's own content (docs/tabletop/scenarios-plan.md, stage S1): a match two
/// greedy agents played over its journal, cut at every round it began, each cut started under an id of its own
/// and played on with the source's decisions to the source's end.
/// </summary>
public sealed class ScenarioOnContentTests
{
    private static readonly GameResources Resources = GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(Path.Combine(AppContext.BaseDirectory, "data")));

    [Theory]
    [InlineData(CombatDeclaration.AtActivation)]
    [InlineData(CombatDeclaration.HiddenIntent)]
    public async Task Every_round_of_a_recorded_match_starts_a_scenario_that_plays_on_to_the_source_s_end(CombatDeclaration declaration)
    {
        const int seed = 21;
        var services = new ServiceCollection().AddApplication().AddInfrastructure(randomSeed: seed);
        services.AddSingleton<IGameResources>(Resources);
        await using var provider = services.BuildServiceProvider();
        var rules = RuleSet.Default.WithCombatDeclaration(declaration);
        var source = new JournaledMatch("source", MatchId.New(), rules, seed, [.. Enumerable.Repeat(Resources.Creatures.First().Id, rules.TeamSize)]);
        var agents = provider.GetRequiredService<IAgentFactory>();
        var seats = new MatchSeats(agents.Create(AgentSpec.Parse("greedy"), rules, new SeededRandomSource(seed + 1)), agents.Create(AgentSpec.Parse("greedy"), rules, new SeededRandomSource(seed + 2)));
        var played = await provider.GetRequiredService<JournaledStepper>().AdvanceAsync(source, seats, TestContext.Current.CancellationToken);
        var entries = (await provider.GetRequiredService<IDecisionJournal>().ReadAsync(source.Run, TestContext.Current.CancellationToken)).Entries;
        var reading = await provider.GetRequiredService<ScenarioRounds>().ReadAsync(source, entries, TestContext.Current.CancellationToken);

        var outcomes = new List<MatchOutcome?>();
        foreach (var cut in reading.Rounds)
        {
            var scenario = await provider.GetRequiredService<ScenarioCutter>().CutAsync(source, entries, cut.Round, TestContext.Current.CancellationToken);
            var started = await provider.GetRequiredService<ScenarioStarter>().StartAsync(scenario.Value, $"scenario-{cut.Round}", MatchId.New(), TestContext.Current.CancellationToken);
            var replay = provider.GetRequiredService<MatchReplay>();
            var held = await replay.ContinueAsync(started.Value.Match, HeldAnswers.None, scenario.Value.After, cut.CutAt, TestContext.Current.CancellationToken);
            var seat = await provider.GetRequiredService<MatchStepper>().SeatAsync(started.Value.Match.MatchId, PlayerSlot.Player1, held.Value, TestContext.Current.CancellationToken);
            outcomes.Add(seat.Value.Board.Outcome);
        }

        reading.StoppedBy.ShouldBeNull();
        reading.Rounds.Count.ShouldBeGreaterThan(2);
        outcomes.ShouldAllBe(outcome => outcome == played.Value.Step.Outcome);
    }
}

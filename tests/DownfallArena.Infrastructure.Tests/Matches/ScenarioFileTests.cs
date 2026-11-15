using DownfallArena.Application;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.Infrastructure.Randomness;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Infrastructure.Tests.Matches;

/// <summary>
/// <c>scenario.json</c> (docs/tabletop/scenarios-plan.md, stage S3): a scenario written and read again is the same
/// scenario, and a file that is not one is refused by name.
/// </summary>
public sealed class ScenarioFileTests
{
    private static readonly GameResources Resources = GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(Path.Combine(AppContext.BaseDirectory, "data")));

    [Fact]
    public async Task A_scenario_read_back_from_its_file_starts_where_it_was_cut_and_plays_on_to_its_source_s_end()
    {
        await using var provider = Provider(7);
        var (scenario, outcome) = await CutAsync(provider, 7);
        var document = new ScenarioDocument(scenario, PlayerSlot.Player2, Resources.Version, "session-1", "abc123", "A title", "A goal");

        var read = ScenarioFile.Read(ScenarioFile.Write(document));

        read.Seat.ShouldBe(PlayerSlot.Player2);
        (read.Content, read.Session, read.Engine, read.Title, read.Goal).ShouldBe((Resources.Version, "session-1", "abc123", "A title", "A goal"));
        read.Scenario.Cut.ShouldBe(scenario.Cut);
        read.Scenario.Source.Rules.ShouldBe(scenario.Source.Rules);
        var started = await provider.GetRequiredService<ScenarioStarter>().StartAsync(read.Scenario, "scenario-1", MatchId.New(), TestContext.Current.CancellationToken);
        started.IsSuccess.ShouldBeTrue();
        var held = await provider.GetRequiredService<MatchReplay>().ContinueAsync(started.Value.Match, HeldAnswers.None, read.Scenario.After, read.Scenario.Cut.CutAt, TestContext.Current.CancellationToken);
        var board = await provider.GetRequiredService<MatchStepper>().SeatAsync(started.Value.Match.MatchId, PlayerSlot.Player1, held.Value, TestContext.Current.CancellationToken);
        board.Value.Board.Outcome.ShouldBe(outcome);
    }

    [Fact]
    public void A_file_of_another_format_is_refused_by_name()
    {
        var refusal = Should.Throw<InvalidDataException>(() => ScenarioFile.Read("""{"format":"downfall.scenario.v9"}"""));

        refusal.Message.ShouldContain("downfall.scenario.v9");
    }

    [Fact]
    public async Task A_file_whose_cut_disagrees_with_the_entries_it_holds_is_refused()
    {
        await using var provider = Provider(7);
        var (scenario, _) = await CutAsync(provider, 7);
        var text = ScenarioFile.Write(new ScenarioDocument(scenario with { Cut = scenario.Cut with { CutAt = scenario.Cut.CutAt + 1 } }, PlayerSlot.Player1, Resources.Version));

        Should.Throw<InvalidDataException>(() => ScenarioFile.Read(text)).Message.ShouldContain("cut");
    }

    /// <summary>Every scenario kept beside these tests still rebuilds on this engine, and its source's answers after the cut still apply.</summary>
    [Fact]
    public async Task Every_kept_scenario_still_holds_on_this_engine()
    {
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "scenarios"), "*.json");
        files.ShouldNotBeEmpty();

        foreach (var file in files)
        {
            var document = ScenarioFile.Read(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
            var schema = Path.Combine(Path.GetTempPath(), "downfall-scenario-tests", Guid.NewGuid().ToString("N"), "game.schema.json");
            Directory.CreateDirectory(Path.GetDirectoryName(schema)!);
            await File.WriteAllTextAsync(schema, document.Schema.ShouldNotBeNull($"{file} carries no content").GetRawText(), TestContext.Current.CancellationToken);
            var resources = GameSchemaBuilder.Load(schema);
            await using var provider = Provider(document.Scenario.Source.Seed, resources);

            var started = await provider.GetRequiredService<ScenarioStarter>().StartAsync(document.Scenario, "kept", MatchId.New(), TestContext.Current.CancellationToken);
            var played = started.IsSuccess
                ? await provider.GetRequiredService<MatchReplay>().ContinueAsync(started.Value.Match, HeldAnswers.None, document.Scenario.After, document.Scenario.Cut.CutAt, TestContext.Current.CancellationToken)
                : null;

            resources.Version.ShouldBe(document.Content, file);
            started.IsSuccess.ShouldBeTrue($"{Path.GetFileName(file)}: {(started.IsFailure ? started.Error.Message : string.Empty)}");
            played!.IsSuccess.ShouldBeTrue($"{Path.GetFileName(file)}: {(played.IsFailure ? played.Error.Message : string.Empty)}");
        }
    }

    private static ServiceProvider Provider(int seed, IGameResources? resources = null)
    {
        var services = new ServiceCollection().AddApplication().AddInfrastructure(randomSeed: seed);
        services.AddSingleton<IGameResources>(resources ?? Resources);
        return services.BuildServiceProvider();
    }

    /// <summary>A greedy mirror on the repository's content, played over its journal and cut at its middle round.</summary>
    private static async Task<(ScenarioCut Scenario, MatchOutcome? Outcome)> CutAsync(ServiceProvider provider, int seed)
    {
        var rules = RuleSet.Default;
        var source = new JournaledMatch("source", MatchId.New(), rules, seed, [.. Enumerable.Repeat(Resources.Creatures.First().Id, rules.TeamSize)]);
        var agents = provider.GetRequiredService<IAgentFactory>();
        var seats = new MatchSeats(agents.Create(AgentSpec.Parse("greedy"), rules, new SeededRandomSource(seed + 1)), agents.Create(AgentSpec.Parse("greedy"), rules, new SeededRandomSource(seed + 2)));
        var played = await provider.GetRequiredService<JournaledStepper>().AdvanceAsync(source, seats, TestContext.Current.CancellationToken);
        var entries = (await provider.GetRequiredService<IDecisionJournal>().ReadAsync(source.Run, TestContext.Current.CancellationToken)).Entries;
        var reading = await provider.GetRequiredService<ScenarioRounds>().ReadAsync(source, entries, TestContext.Current.CancellationToken);
        var cut = await provider.GetRequiredService<ScenarioCutter>().CutAsync(source, entries, reading.Rounds[reading.Rounds.Count / 2].Round, TestContext.Current.CancellationToken);
        return (cut.Value, played.Value.Step.Outcome);
    }
}

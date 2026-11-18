using DownfallArena.Application;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.Infrastructure.Tests.Invariants;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Infrastructure.Tests.Matches.Bugs;

/// <summary>
/// A bug scenario replayed for its test (docs/tabletop/feedback-plan.md, "The factory", R6): a file of
/// <c>scenarios/bugs/</c>, rebuilt at its cut on the content it carries, then taken through a number of its recorded
/// entries past the cut, each of which must still apply. The test then asserts with Shouldly what the rule says, on
/// both seats' boards and on the events the replay raised past the cut.
/// </summary>
internal static class BugScenario
{
    /// <summary>
    /// The scenario at <paramref name="file" />, relative to the tests' output (<c>scenarios/bugs/812-bleed.json</c>), taken
    /// through its first <paramref name="entries" /> entries past the cut.
    /// </summary>
    public static async Task<BugReplay> ReplayAsync(string file, int entries)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = ScenarioFile.Read(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, file), cancellation));
        var scenario = document.Scenario;
        entries.ShouldBeInRange(0, scenario.After.Count, $"{file} holds {scenario.After.Count} entries past its cut");
        var schema = Path.Combine(Path.GetTempPath(), "downfall-bug-scenarios", Guid.NewGuid().ToString("N"), "game.schema.json");
        Directory.CreateDirectory(Path.GetDirectoryName(schema)!);
        await File.WriteAllTextAsync(schema, document.Schema.ShouldNotBeNull($"{file} carries no content").GetRawText(), cancellation);
        GameResources resources;
        try
        {
            resources = GameSchemaBuilder.Load(schema);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(schema)!, recursive: true);
        }

        resources.Version.ShouldBe(document.Content, file);

        var log = new MatchEventLog();
        var services = new ServiceCollection().AddApplication().AddInfrastructure(randomSeed: scenario.Source.Seed);
        services.AddSingleton<IGameResources>(resources);
        services.AddSingleton<IDomainEventListener>(log);
        await using var provider = services.BuildServiceProvider();

        var started = await provider.GetRequiredService<ScenarioStarter>().StartAsync(scenario, "bug", MatchId.New(), cancellation);
        started.IsSuccess.ShouldBeTrue($"{file} does not rebuild at its cut: {(started.IsFailure ? started.Error.Message : string.Empty)}");
        var match = started.Value.Match.MatchId;
        var before = log.Of(match).Count;
        var held = await provider.GetRequiredService<MatchReplay>().ContinueAsync(started.Value.Match, HeldAnswers.None, [.. scenario.After.Take(entries)], scenario.Cut.CutAt, cancellation);
        held.IsSuccess.ShouldBeTrue($"{file}: {(held.IsFailure ? held.Error.Message : string.Empty)}");

        var stepper = provider.GetRequiredService<MatchStepper>();
        var player1 = await stepper.SeatAsync(match, PlayerSlot.Player1, held.Value, cancellation);
        var player2 = await stepper.SeatAsync(match, PlayerSlot.Player2, held.Value, cancellation);
        player1.IsSuccess.ShouldBeTrue($"{file}: {(player1.IsFailure ? player1.Error.Message : string.Empty)}");
        player2.IsSuccess.ShouldBeTrue($"{file}: {(player2.IsFailure ? player2.Error.Message : string.Empty)}");
        return new BugReplay(document, resources, player1.Value.Board, player2.Value.Board, [.. log.Of(match).Skip(before)]);
    }
}

/// <summary>Where a bug scenario stands after the entries its test replayed: each seat's board and the events raised past the cut.</summary>
internal sealed record BugReplay(ScenarioDocument Document, IGameResources Resources, PlayerBoardState Player1, PlayerBoardState Player2, IReadOnlyList<IMatchEvent> Events)
{
    public PlayerBoardState Of(PlayerSlot slot) => slot == PlayerSlot.Player1 ? Player1 : Player2;
}

using System.Text.Json;
using System.Text.Json.Nodes;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Learning;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// What a table stepped over its journal leaves on disk is what the same table left when a driver played it
/// (ADR 0159): the same steps under the same names, the same episodes, the same boards in its trace. Nothing
/// sits between a stepped match and its seats to record through, so the recording is shown every decision
/// instead, and this is what holds the two to each other. The driven table is built the way the host built
/// one before, since the composer no longer does.
/// </summary>
public sealed class SteppedRecordingTests : IDisposable
{
    private const int Seed = 7;

    private readonly HostedTables _stepped = new(recording: true);
    private readonly HostedTables _driven = new(recording: true);
    private readonly TableRegistry _registry = new();
    private readonly CancellationTokenSource _stopping = new();

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
        _registry.Dispose();
        _driven.Dispose();
        _stepped.Dispose();
    }

    [Fact]
    public async Task A_stepped_table_of_bots_leaves_the_dataset_and_the_boards_a_driven_one_left()
    {
        var table = await _stepped.Composer.ComposeAsync(HostedTables.Bots with { Seed = Seed }, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();
        var played = await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 500 && !table.IsFinished; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        table.IsFinished.ShouldBeTrue();
        var (run, session) = await DrivenAsync();
        var outcome = await session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var board = await session.Queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, PlayerSlot.Player1), TestContext.Current.CancellationToken);
        await run.FinishAsync(session.MatchId, board.Value, TestContext.Current.CancellationToken);

        var stepped = new FileArtifactStore(_stepped.RunsDirectory).Reader(table.Id);
        var driven = new FileArtifactStore(_driven.RunsDirectory).Reader(run.SessionId);

        played.Value.ShouldBe(outcome.Value);
        // Seat by seat: the driver asks both seats what they answer at once on two threads, so which seat's
        // step it wrote first was never the same from one run to the next. A seat's own steps are in order.
        foreach (var slot in new[] { "Player1", "Player2" })
        {
            var steps = await LinesAsync(stepped, RunRecorder.StepsFile, slot);
            var left = await LinesAsync(driven, RunRecorder.StepsFile, slot);
            steps.Count.ShouldBeGreaterThan(5);
            for (var step = 0; step < Math.Min(steps.Count, left.Count); step++)
            {
                steps[step].ShouldBe(left[step], $"step {step + 1} of {slot}");
            }

            steps.Count.ShouldBe(left.Count);
        }

        (await LinesAsync(stepped, RunRecorder.EpisodesFile)).ShouldBe(await LinesAsync(driven, RunRecorder.EpisodesFile));
        (await ManifestAsync(stepped)).ShouldBe(await ManifestAsync(driven));
        var boards = await BoardsAsync(stepped, table.Session.MatchId);
        var held = await BoardsAsync(driven, session.MatchId);
        for (var entry = 0; entry < Math.Min(boards.Count, held.Count); entry++)
        {
            boards[entry].ShouldBe(held[entry], $"entry {entry}");
        }

        boards.Count.ShouldBe(held.Count);
    }

    /// <summary>The table of two greedy bots the composer builds, played by the blocking driver and recorded through its seats.</summary>
    private async Task<(PlaytestRun Run, TableSession Session)> DrivenAsync()
    {
        var services = _driven.Services;
        var resources = services.GetRequiredService<IGameResources>();
        var spec = AgentSpec.Parse("greedy");
        var run = PlaytestRun.Open(
            new FileArtifactStore(_driven.RunsDirectory),
            new PlaytestSetup(resources, HostedTables.Rules, Seed, spec.ToString(), spec.ToString()),
            services.GetRequiredService<MatchTraceRecorder>(),
            TimeProvider.System);
        await run.StartAsync(CatalogueProjection.Build(resources, HostedTables.Rules), TestContext.Current.CancellationToken);
        var session = await TableSession.StartAsync(
            services,
            HostedTables.Rules,
            Seed,
            new SeatAgent(new Occupant(Bot(services, spec, PlayerSlot.Player1), spec.ToString())),
            new SeatAgent(new Occupant(Bot(services, spec, PlayerSlot.Player2), spec.ToString())),
            new TableStart(run.Wrap),
            _stopping.Token);
        return (run, session);
    }

    /// <summary>A seat's bot, drawing from the source a table derives for that seat from its seed.</summary>
    private static IPlayerAgent Bot(IServiceProvider services, AgentSpec spec, PlayerSlot slot) =>
        services.GetRequiredService<IAgentFactory>().Create(
            spec,
            HostedTables.Rules,
            services.GetRequiredService<IRandomSourceFactory>().Create(unchecked((Seed * 31) + (slot == PlayerSlot.Player1 ? 1 : 2))));

    /// <summary>A file's lines without what names the match: the id is drawn when a table opens.</summary>
    /// <param name="slot">The seat whose lines to keep, or every line.</param>
    private static async Task<IReadOnlyList<string>> LinesAsync(DownfallArena.Application.Learning.Ports.IArtifactReader reader, string file, string? slot = null)
    {
        var text = (await reader.ReadTextAsync(file, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        return [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .Where(record => slot is null || record["slot"]!.GetValue<string>() == slot)
            .Select(record =>
            {
                record.Remove("matchId");
                return record.ToJsonString();
            })];
    }

    /// <summary>What the manifest says was played and how much of it, without when.</summary>
    private static async Task<string> ManifestAsync(DownfallArena.Application.Learning.Ports.IArtifactReader reader)
    {
        var manifest = JsonNode.Parse((await reader.ReadTextAsync(RunRecorder.ManifestFile, TestContext.Current.CancellationToken)).ShouldNotBeNull())!.AsObject();
        manifest.Remove("createdAt");
        return manifest.ToJsonString();
    }

    /// <summary>The boards of a trace and where each was, the match unnamed: the events beside them carry ids drawn when the seats joined.</summary>
    private static async Task<IReadOnlyList<string>> BoardsAsync(DownfallArena.Application.Learning.Ports.IArtifactReader reader, DownfallArena.SharedKernel.Identifiers.MatchId matchId)
    {
        using var trace = JsonDocument.Parse((await reader.ReadTextAsync($"{RunRecorder.TracesDirectory}/{matchId}.json", TestContext.Current.CancellationToken)).ShouldNotBeNull());
        return [.. trace.RootElement.GetProperty("entries").EnumerateArray().Select(entry =>
            string.Join('|', new[] { "sequence", "round", "subPhase", "player1", "player2" }.Select(field => entry.TryGetProperty(field, out var value) ? value.GetRawText() : string.Empty))
                .Replace(matchId.ToString(), "match", StringComparison.Ordinal))];
    }
}

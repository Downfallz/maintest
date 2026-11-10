using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// What a host recorded before ADR 0159 is what its journal reads: the <c>decisions.jsonl</c> a table played
/// by the blocking driver wrote, read by the journal and submitted again through the stepper, with no seat, no
/// session and no recording around it, gives the match that host was holding. It is what lets a table that
/// was open when the host changed be picked up after. The table here is built the way that host built one,
/// since the composer no longer does.
/// </summary>
public sealed class RecordedTableReplayTests : IDisposable
{
    private readonly HostedTables _host = new(recording: true);
    private readonly HostedTables _other = new();
    private readonly CancellationTokenSource _stopping = new();

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
        _other.Dispose();
        _host.Dispose();
    }

    [Fact]
    public async Task A_finished_table_s_record_rebuilds_the_match_the_host_played()
    {
        var (run, session, _) = await Driven(person: false);
        (await session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        await run.Journal.Written;
        var journal = new FileDecisionJournal(_host.RunsDirectory, TimeProvider.System);

        var reading = await journal.ReadAsync(run.SessionId, TestContext.Current.CancellationToken);
        var rebuilt = await Replay().RebuildAsync(Recorded(run, session), reading.Entries, TestContext.Current.CancellationToken);

        reading.Decisions.ShouldBe(await WrittenByTheHost(run));
        reading.Decisions.Count.ShouldBeGreaterThan(10);
        rebuilt.IsSuccess.ShouldBeTrue(rebuilt.IsFailure ? rebuilt.Error.Message : string.Empty);
        rebuilt.Value.IsEmpty.ShouldBeTrue();
        foreach (var slot in new[] { PlayerSlot.Player1, PlayerSlot.Player2 })
        {
            (await Rebuilt(session.MatchId, slot)).ShouldBe(await Held(session, slot));
        }
    }

    [Fact]
    public async Task A_table_waiting_for_a_person_rebuilds_to_the_same_question_with_the_bot_s_answers_held()
    {
        var (run, session, person) = await Driven(person: true);
        var journal = new FileDecisionJournal(_host.RunsDirectory, TimeProvider.System);
        var reading = await journal.ReadAsync(run.SessionId, TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 300 && (person!.Waiting is null || reading.Decisions.Count < HostedTables.Rules.EvolutionPicksPerOpportunity); attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
            await run.Journal.Written;
            reading = await journal.ReadAsync(run.SessionId, TestContext.Current.CancellationToken);
        }

        var waiting = person!.Waiting.ShouldNotBeNull();

        var rebuilt = await Replay().RebuildAsync(Recorded(run, session), reading.Entries, TestContext.Current.CancellationToken);

        reading.Decisions.ShouldBe(await WrittenByTheHost(run));
        reading.Decisions.ShouldNotBeEmpty();
        reading.Decisions.ShouldAllBe(decision => decision.Slot == PlayerSlot.Player2);
        rebuilt.IsSuccess.ShouldBeTrue(rebuilt.IsFailure ? rebuilt.Error.Message : string.Empty);
        rebuilt.Value.Player2.ShouldBe([.. reading.Decisions.Select(decision => decision.Decision)]);
        var view = await _other.Services.GetRequiredService<MatchStepper>().SeatAsync(session.MatchId, PlayerSlot.Player1, rebuilt.Value, TestContext.Current.CancellationToken);
        view.Value.Asked.ShouldBe(new SeatQuestion(waiting.Kind, waiting.Creature));
    }

    /// <summary>A recorded table played by the blocking driver: bots, or a person in seat 1 who is asked and never answers.</summary>
    private async Task<(PlaytestRun Run, TableSession Session, HumanSeat? Person)> Driven(bool person)
    {
        var resources = _host.Services.GetRequiredService<IGameResources>();
        var run = PlaytestRun.Open(
            new FileArtifactStore(_host.RunsDirectory),
            new PlaytestSetup(resources, HostedTables.Rules, Seed, person ? "human:mk" : "greedy", "greedy"),
            _host.Services.GetRequiredService<MatchTraceRecorder>(),
            TimeProvider.System);
        await run.StartAsync(CatalogueProjection.Build(resources, HostedTables.Rules), TestContext.Current.CancellationToken);
        var human = person ? new HumanSeat(_stopping.Token) : null;
        var session = await TableSession.StartAsync(
            _host.Services,
            HostedTables.Rules,
            Seed,
            new SeatAgent(human is null ? new Occupant(new GreedyAgent(resources, HostedTables.Rules), "greedy") : new Occupant(human, "human:mk")),
            new SeatAgent(new Occupant(new GreedyAgent(resources, HostedTables.Rules), "greedy")),
            new TableStart(run.Wrap),
            _stopping.Token);
        return (run, session, human);
    }

    private const int Seed = 7;

    /// <summary>The match of a table as its record describes it, to a process that holds none of it.</summary>
    private JournaledMatch Recorded(PlaytestRun run, TableSession session)
    {
        var creature = _other.Services.GetRequiredService<IGameResources>().Creatures.First().Id;
        return new JournaledMatch(run.SessionId, session.MatchId, session.Rules, Seed, [.. Enumerable.Repeat(creature, session.Rules.TeamSize)]);
    }

    private MatchReplay Replay() => _other.Services.GetRequiredService<MatchReplay>();

    /// <summary>The decisions as the store's own reader gives them, which is how that host rebuilt a table (ADR 0091).</summary>
    private async Task<IReadOnlyList<SeatDecision>> WrittenByTheHost(PlaytestRun run) =>
        [.. (await _host.Stored!.JournalAsync(run.SessionId, TestContext.Current.CancellationToken))
            .Where(entry => !entry.IsSwap)
            .Select(entry => new SeatDecision(entry.PlayerSlot, entry.ToDecision()))];

    private static async Task<string> Held(TableSession session, PlayerSlot slot)
    {
        var board = await session.Queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, slot), TestContext.Current.CancellationToken);
        return JsonSerializer.Serialize(board.Value, ArtifactJson.LineOptions);
    }

    private async Task<string> Rebuilt(MatchId matchId, PlayerSlot slot)
    {
        var board = await _other.Services.GetRequiredService<MatchQueryHandlers>().GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(matchId, slot), TestContext.Current.CancellationToken);
        return JsonSerializer.Serialize(board.Value, ArtifactJson.LineOptions);
    }
}

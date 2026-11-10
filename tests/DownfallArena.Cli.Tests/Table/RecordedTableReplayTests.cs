using System.Text.Json;
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
/// What the table records today is what ADR 0159's journal reads: the <c>decisions.jsonl</c> a host wrote,
/// read by the journal and submitted again through the stepper, with no seat, no session and no recording
/// around it, gives the match the host is holding. It is what lets a table recorded before the host changes
/// be picked up after.
/// </summary>
public sealed class RecordedTableReplayTests : IDisposable
{
    private readonly HostedTables _host = new(recording: true);
    private readonly HostedTables _other = new();
    private readonly TableRegistry _registry = new();

    public void Dispose()
    {
        _registry.Dispose();
        _other.Dispose();
        _host.Dispose();
    }

    [Fact]
    public async Task A_finished_table_s_record_rebuilds_the_match_the_host_played()
    {
        var table = await Opened(HostedTables.Bots);
        (await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        await table.Run!.Journal.Written;
        var journal = new FileDecisionJournal(_host.RunsDirectory, TimeProvider.System);

        var reading = await journal.ReadAsync(table.Id, TestContext.Current.CancellationToken);
        var rebuilt = await Replay().RebuildAsync(Recorded(table), reading.Entries, TestContext.Current.CancellationToken);

        reading.Decisions.ShouldBe(await WrittenByTheHost(table));
        reading.Decisions.Count.ShouldBeGreaterThan(10);
        rebuilt.IsSuccess.ShouldBeTrue(rebuilt.IsFailure ? rebuilt.Error.Message : string.Empty);
        rebuilt.Value.IsEmpty.ShouldBeTrue();
        foreach (var slot in new[] { PlayerSlot.Player1, PlayerSlot.Player2 })
        {
            (await Rebuilt(table.Session.MatchId, slot)).ShouldBe(await Held(table, slot));
        }
    }

    [Fact]
    public async Task A_table_waiting_for_a_person_rebuilds_to_the_same_question_with_the_bot_s_answers_held()
    {
        var table = await Opened(HostedTables.OnePerson);
        var person = table.Seats[0].Person.ShouldNotBeNull();
        for (var attempt = 0; attempt < 300 && person.Waiting is null; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        var waiting = person.Waiting.ShouldNotBeNull();
        await table.Run!.Journal.Written;
        var journal = new FileDecisionJournal(_host.RunsDirectory, TimeProvider.System);

        var reading = await journal.ReadAsync(table.Id, TestContext.Current.CancellationToken);
        var rebuilt = await Replay().RebuildAsync(Recorded(table), reading.Entries, TestContext.Current.CancellationToken);

        reading.Decisions.ShouldBe(await WrittenByTheHost(table));
        reading.Decisions.ShouldNotBeEmpty();
        reading.Decisions.ShouldAllBe(decision => decision.Slot == PlayerSlot.Player2);
        rebuilt.IsSuccess.ShouldBeTrue(rebuilt.IsFailure ? rebuilt.Error.Message : string.Empty);
        rebuilt.Value.Player2.ShouldBe([.. reading.Decisions.Select(decision => decision.Decision)]);
        var view = await _other.Services.GetRequiredService<MatchStepper>().SeatAsync(table.Session.MatchId, PlayerSlot.Player1, rebuilt.Value, TestContext.Current.CancellationToken);
        view.Value.Asked.ShouldBe(new SeatQuestion(waiting.Kind, waiting.Creature));
    }

    private async Task<PlayedTable> Opened(TableRequest request)
    {
        var table = await _host.Composer.ComposeAsync(request, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();
        await table.RecordAsync(_registry.Codes, TestContext.Current.CancellationToken);
        foreach (var seat in table.Seats.Where(seat => seat.Person is not null))
        {
            (await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token)).Status.ShouldBe(200);
        }

        return table;
    }

    /// <summary>The match of a table as its record describes it, to a process that holds none of it.</summary>
    private JournaledMatch Recorded(PlayedTable table)
    {
        var rules = table.Session.Rules;
        var creature = _other.Services.GetRequiredService<IGameResources>().Creatures.First().Id;
        return new JournaledMatch(table.Id, table.Session.MatchId, rules, table.Request!.Seed!.Value, [.. Enumerable.Repeat(creature, rules.TeamSize)]);
    }

    private MatchReplay Replay() => _other.Services.GetRequiredService<MatchReplay>();

    /// <summary>The decisions as the host's own reader gives them, which is how a host rebuilds a table today (ADR 0091).</summary>
    private async Task<IReadOnlyList<SeatDecision>> WrittenByTheHost(PlayedTable table) =>
        [.. (await _host.Stored!.JournalAsync(table.Id, TestContext.Current.CancellationToken))
            .Where(entry => !entry.IsSwap)
            .Select(entry => new SeatDecision(entry.PlayerSlot, entry.ToDecision()))];

    private static async Task<string> Held(PlayedTable table, PlayerSlot slot)
    {
        var board = await table.Session.Queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(table.Session.MatchId, slot), TestContext.Current.CancellationToken);
        return JsonSerializer.Serialize(board.Value, ArtifactJson.LineOptions);
    }

    private async Task<string> Rebuilt(MatchId matchId, PlayerSlot slot)
    {
        var board = await _other.Services.GetRequiredService<MatchQueryHandlers>().GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(matchId, slot), TestContext.Current.CancellationToken);
        return JsonSerializer.Serialize(board.Value, ArtifactJson.LineOptions);
    }
}

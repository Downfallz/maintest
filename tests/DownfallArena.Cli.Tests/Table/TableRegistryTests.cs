using System.Text;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Tables.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A host of tables finds each one by what a request carries (ADR 0081), as a cache over the index of the tables
/// (ADR 0161). What these hold is that a token reaches its own table and no other, that a code does the same while
/// its seat still has it, that the cap counts every host's open tables, and that a table nobody is at is let go of.
/// </summary>
public sealed class TableRegistryTests : IDisposable
{
    /// <summary>A table on another host, as the index names it.</summary>
    private const string Elsewhere = "20261110-120000-elsewhere";

    private readonly HostedTables _hosted = new();

    public void Dispose() => _hosted.Dispose();

    [Fact]
    public async Task A_seat_token_names_its_table_and_no_other()
    {
        using var registry = _hosted.Registry();
        var first = await Added(registry, await Composed(HostedTables.OnePerson));
        var second = await Added(registry, await Composed(HostedTables.OnePerson));

        (await registry.ByTokenAsync(first.Seats[0].Token, TestContext.Current.CancellationToken)).ShouldBeSameAs(first);
        (await registry.ByTokenAsync(second.Seats[0].Token, TestContext.Current.CancellationToken)).ShouldBeSameAs(second);
        (await registry.ByTokenAsync(first.Pilot.Token, TestContext.Current.CancellationToken)).ShouldBeSameAs(first);
        (await registry.ByTokenAsync("nobody-s-token", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await registry.ByTokenAsync(null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task A_code_typed_at_the_host_reaches_the_seat_it_was_given_to()
    {
        using var registry = _hosted.Registry();
        var first = await Added(registry, await Composed(HostedTables.OnePerson));
        var second = await Added(registry, await Composed(HostedTables.OnePerson));
        var code = second.Seats[0].Code.ShouldNotBeNull();

        var answer = await registry.AnswerAsync($"{JoinCodes.Prefix}{code}", TestContext.Current.CancellationToken);

        answer.Status.ShouldBe(303);
        answer.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Location" && header.Value == $"/?player1={second.Seats[0].Invitation}");
        first.Seats[0].Code.ShouldNotBe(code);
        first.Seats[1].Code.ShouldBeNull("a bot's seat has nobody to hand a code to");
    }

    [Fact]
    public async Task A_code_read_slightly_wrong_still_reaches_its_seat()
    {
        using var registry = _hosted.Registry();
        var table = await Added(registry, await Composed(HostedTables.OnePerson));
        var code = table.Seats[0].Code.ShouldNotBeNull();

        var answer = await registry.AnswerAsync($"{JoinCodes.Prefix}{code.ToLowerInvariant()}/", TestContext.Current.CancellationToken);

        answer.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Location" && header.Value == $"/?player1={table.Seats[0].Invitation}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ZZZZZZZZ")]
    [InlineData("../api/seat/player1")]
    public async Task A_code_no_seat_was_given_names_no_seat(string code)
    {
        using var registry = _hosted.Registry();
        var table = await Added(registry, await Composed(HostedTables.OnePerson));

        var answer = await registry.AnswerAsync($"{JoinCodes.Prefix}{code}", TestContext.Current.CancellationToken);

        answer.ShouldNameNoSeat();
        Encoding.UTF8.GetString(answer.Body).ShouldNotContain(table.Seats[0].Token);
    }

    /// <summary>
    /// The index is a pointer and the seat is what says: a seat offered again has a new code, and the old one,
    /// which the index still names until the table's record is written, reaches nothing.
    /// </summary>
    [Fact]
    public async Task A_code_answers_only_while_its_seat_still_has_it()
    {
        using var registry = _hosted.Registry();
        var table = await Added(registry, await Composed(HostedTables.OnePerson));
        var seat = table.Seats[0];
        var before = seat.Code.ShouldNotBeNull();

        await registry.RekeyAsync(table, seat, SeatKeys.Offered(table.Id), TestContext.Current.CancellationToken);

        (await registry.AnswerAsync($"{JoinCodes.Prefix}{before}", TestContext.Current.CancellationToken)).ShouldNameNoSeat();
        seat.Code.ShouldNotBeNull().ShouldNotBe(before);
        (await registry.AnswerAsync($"{JoinCodes.Prefix}{seat.Code}", TestContext.Current.CancellationToken)).Headers.ShouldNotBeNull()
            .ShouldContain(header => header.Key == "Location" && header.Value == $"/?player1={seat.Invitation}");
    }

    /// <summary>
    /// The codes are every host's: one the index gives another table is not taken again, even when a seat asks for
    /// it back, or a code typed at any host would reach two seats.
    /// </summary>
    [Fact]
    public async Task A_code_another_table_holds_in_the_index_is_not_taken_again()
    {
        using var registry = _hosted.Registry();
        var table = await Added(registry, await Composed(HostedTables.OnePerson));
        var taken = JoinCodes.Draw();
        await _hosted.Index.AddCodeAsync(new IndexedCode(taken, Elsewhere, "player1"), TestContext.Current.CancellationToken);

        await registry.RekeyAsync(table, table.Seats[0], SeatKeys.Offered(table.Id) with { Code = taken }, TestContext.Current.CancellationToken);

        table.Seats[0].Code.ShouldNotBeNull().ShouldNotBe(taken);
        (await _hosted.Index.CodeAsync(taken, TestContext.Current.CancellationToken)).ShouldBe(new IndexedCode(taken, Elsewhere, "player1"));
        (await _hosted.Index.CodeAsync(table.Seats[0].Code!, TestContext.Current.CancellationToken)).ShouldBe(new IndexedCode(table.Seats[0].Code!, table.Id, "player1"));
    }

    /// <summary>A code the index names, typed at a host that does not hold its table, picks the table up from its record (ADR 0161).</summary>
    [Fact]
    public async Task A_code_typed_at_a_host_that_does_not_hold_its_table_reaches_its_seat()
    {
        using var recording = new HostedTables(recording: true);
        var table = await recording.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        using (var before = recording.Registry())
        {
            await Added(before, table);
            await table.RecordAsync(TestContext.Current.CancellationToken);
        }

        using var registry = recording.Registry();
        var answer = await registry.AnswerAsync($"{JoinCodes.Prefix}{table.Seats[0].Code}", TestContext.Current.CancellationToken);

        answer.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Location" && header.Value == $"/?player1={table.Seats[0].Invitation}");
        registry.Held(table.Id).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_table_removed_is_found_by_nothing_it_used_to_be_found_by()
    {
        using var registry = _hosted.Registry();
        var table = await Added(registry, await Composed(HostedTables.OnePerson));
        var token = table.Seats[0].Token;
        var code = table.Seats[0].Code!;

        (await registry.RemoveAsync(table.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();

        (await registry.ByTokenAsync(token, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await registry.ByIdAsync(table.Id, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await registry.AnswerAsync($"{JoinCodes.Prefix}{code}", TestContext.Current.CancellationToken)).ShouldNameNoSeat();
        (await registry.RemoveAsync(table.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();

        // Removing stops the match: the person's seat is released and the driver ends without an outcome.
        await Should.ThrowAsync<OperationCanceledException>(() => table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    /// <summary>A table closed on a recording host is closed in its record, so no host picks it up again.</summary>
    [Fact]
    public async Task A_table_removed_is_not_picked_up_again_from_its_record()
    {
        using var recording = new HostedTables(recording: true);
        using var registry = recording.Registry();
        var table = await Added(registry, await recording.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken));
        await table.RecordAsync(TestContext.Current.CancellationToken);

        await registry.RemoveAsync(table.Id, TestContext.Current.CancellationToken);
        await table.Closing;
        using var after = recording.Registry();

        (await after.ByTokenAsync(table.Seats[0].Token, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task The_host_takes_only_so_many_tables_under_way_and_a_finished_one_does_not_count()
    {
        using var registry = _hosted.Registry(mostUnderWay: 1);
        var finished = await Composed(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Added(registry, finished);
        await Added(registry, await Composed(HostedTables.OnePerson));

        var another = await registry.ReserveAsync(TestContext.Current.CancellationToken);

        another.ShouldBeNull("one table under way is as many as this host takes, and a finished table is not under way");
    }

    /// <summary>The cap is across the index: a table another host holds is one under way all the same.</summary>
    [Fact]
    public async Task An_open_table_the_index_lists_counts_against_a_host_that_does_not_hold_it()
    {
        using var other = _hosted.Registry();
        var table = await Added(other, await Composed(HostedTables.OnePerson));
        await table.RecordAsync(TestContext.Current.CancellationToken);
        using var registry = _hosted.Registry(mostUnderWay: 1);

        var place = await registry.ReserveAsync(TestContext.Current.CancellationToken);

        place.ShouldBeNull("the one table under way is another host's, and it counts here too");
    }

    /// <summary>The open tables are the index's and this host's, each once: the one held here as it stands now.</summary>
    [Fact]
    public async Task The_open_tables_are_the_index_s_and_those_held_here()
    {
        using var other = _hosted.Registry();
        var theirs = await Added(other, await Composed(HostedTables.OnePerson));
        await theirs.RecordAsync(TestContext.Current.CancellationToken);
        using var registry = _hosted.Registry();
        var mine = await Added(registry, await Composed(HostedTables.OnePerson));
        await mine.RecordAsync(TestContext.Current.CancellationToken);

        var open = await registry.OpenAsync(TestContext.Current.CancellationToken);

        open.Select(table => table.Id).ShouldBe([theirs.Id, mine.Id], ignoreOrder: true);
        open.Single(table => table.Id == mine.Id).Held.ShouldBeSameAs(mine);
        open.Single(table => table.Id == theirs.Id).Held.ShouldBeNull();
    }

    [Fact]
    public async Task A_finished_table_is_let_go_of_an_hour_after_the_last_request_and_a_live_one_is_not()
    {
        using var registry = _hosted.Registry();
        var finished = await Composed(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var live = await Composed(HostedTables.OnePerson);
        await Added(registry, finished);
        await Added(registry, live);

        (await registry.SweepAsync(finished.CreatedAt + TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken)).ShouldBeEmpty();
        var gone = await registry.SweepAsync(finished.CreatedAt + TableRegistry.FinishedFor, TestContext.Current.CancellationToken);

        gone.ShouldBe([finished.Id]);
        registry.Held(live.Id).ShouldBeSameAs(live);
        registry.HeldTables().ShouldBe([live]);
    }

    [Fact]
    public async Task Letting_go_of_the_idle_tables_lets_go_of_the_ones_held_here_that_nobody_is_at()
    {
        using var registry = _hosted.Registry();
        var finished = await Composed(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var live = await Composed(HostedTables.OnePerson);
        await Added(registry, finished);
        await Added(registry, live);

        var gone = registry.LetGoOfIdle(live.CreatedAt + TableRegistry.NeverBegunFor + TableRegistry.FinishedFor);

        gone.ShouldBe([finished.Id, live.Id], ignoreOrder: true);
        registry.HeldTables().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_game_that_began_keeps_its_seat_after_a_long_break()
    {
        using var registry = _hosted.Registry();
        var idle = await Added(registry, await Composed(HostedTables.OnePerson));
        idle.Seats[0].Person!.Arrive();
        idle.Session.Begin();

        (await registry.SweepAsync(idle.CreatedAt + TimeSpan.FromDays(30), TestContext.Current.CancellationToken)).ShouldBeEmpty();
        (await registry.ByTokenAsync(idle.Seats[0].Token, TestContext.Current.CancellationToken)).ShouldBeSameAs(idle);
        idle.Session.Outcome.IsCompleted.ShouldBeFalse();
    }

    /// <summary>
    /// A table that never began -- nobody came, or its second player never did -- is closed once nobody has
    /// asked anything of it for a night, or a lobby anyone may open tables from fills the host for good (ADR 0115).
    /// </summary>
    [Fact]
    public async Task A_table_that_never_began_is_closed_after_a_night_without_a_request()
    {
        using var registry = _hosted.Registry();
        var idle = await Added(registry, await Composed(HostedTables.OnePerson));

        idle.Touch(idle.CreatedAt + TableRegistry.NeverBegunFor - TimeSpan.FromMinutes(1));
        (await registry.SweepAsync(idle.CreatedAt + TableRegistry.NeverBegunFor, TestContext.Current.CancellationToken)).ShouldBeEmpty("a request a minute ago is somebody still waiting");

        (await registry.SweepAsync(idle.CreatedAt + TableRegistry.NeverBegunFor + TableRegistry.NeverBegunFor, TestContext.Current.CancellationToken)).ShouldBe([idle.Id]);
        (await registry.ByTokenAsync(idle.Seats[0].Token, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// A table that never began and that no host holds any more is closed by the sweep of any host, through its
    /// record: nobody picks it up to let it go, and a lobby would otherwise list it for good (ADR 0115, ADR 0161).
    /// </summary>
    [Fact]
    public async Task A_table_nobody_holds_that_never_began_is_closed_through_its_record_after_a_night()
    {
        using var recording = new HostedTables(recording: true);
        var table = await recording.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        using (var before = recording.Registry())
        {
            await Added(before, table);
            await table.RecordAsync(TestContext.Current.CancellationToken);
        }

        using var registry = recording.Registry();
        (await registry.SweepAsync(table.CreatedAt + TableRegistry.NeverBegunFor - TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken)).ShouldBeEmpty();
        var swept = await registry.SweepAsync(table.CreatedAt + TableRegistry.NeverBegunFor, TestContext.Current.CancellationToken);

        swept.ShouldBe([table.Id]);
        (await recording.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Closed);
        (await recording.Index.OpenAsync(0, 10, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    /// <summary>
    /// A place is held before a table is composed, and counts until the table takes it or the holder lets go:
    /// two requests for the last place cannot both compose a table.
    /// </summary>
    [Fact]
    public async Task A_place_reserved_counts_against_the_host_until_it_is_taken_or_given_back()
    {
        using var registry = _hosted.Registry(mostUnderWay: 1);

        var held = (await registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        (await registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldBeNull("the one place is held");
        held.Dispose();

        using var again = (await registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var table = await Composed(HostedTables.OnePerson);
        await registry.AddAsync(table, again, TestContext.Current.CancellationToken);
        registry.Held(table.Id).ShouldBeSameAs(table);
        (await registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldBeNull("the place is the table's now");
        again.Dispose();
        (await registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldBeNull("disposing a spent reservation gives nothing back");
    }

    /// <summary>
    /// A table let go of is forgotten by the host's stores too: the repository that held its match and the
    /// recorder that kept its feed. A host that plays for days would otherwise keep every match it ever played.
    /// </summary>
    [Fact]
    public async Task A_table_let_go_of_is_forgotten_by_the_repository_and_the_trace_recorder()
    {
        var table = await Composed(HostedTables.Bots);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var repository = _hosted.Services.GetRequiredService<IMatchRepository>();
        var recorder = _hosted.Services.GetRequiredService<MatchTraceRecorder>();
        (await repository.FindAsync(table.Session.MatchId, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        recorder.EntriesOf(table.Session.MatchId).ShouldNotBeEmpty();

        table.Dispose();
        await Forgotten(repository, table.Session.MatchId);

        recorder.EntriesOf(table.Session.MatchId).ShouldBeEmpty();
    }

    /// <summary>
    /// A table's bots draw from sources derived from its seed, so the seed a run stamp carries replays the
    /// session whatever other tables the host played before it.
    /// </summary>
    [Fact]
    public async Task Two_tables_opened_with_one_seed_play_the_same_match()
    {
        var dice = new TableRequest(AgentSpec.Parse("random"), AgentSpec.Parse("random"), Who: null, Handover: null, Seed: 11);
        var recorder = _hosted.Services.GetRequiredService<MatchTraceRecorder>();
        using var first = await Composed(dice);
        var firstOutcome = await first.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        using var second = await Composed(dice);
        var secondOutcome = await second.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var one = recorder.EntriesOf(first.Session.MatchId).Select(entry => entry.Event.GetType().Name).ToList();
        var other = recorder.EntriesOf(second.Session.MatchId).Select(entry => entry.Event.GetType().Name).ToList();

        one.ShouldNotBeEmpty();
        other.ShouldBe(one);
        secondOutcome.Value.Winner.ShouldBe(firstOutcome.Value.Winner);
    }

    /// <summary>The forgetting runs once the driver has stopped, on its own task, so a test waits for it.</summary>
    private static async Task Forgotten(IMatchRepository repository, MatchId matchId)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            if (await repository.FindAsync(matchId, TestContext.Current.CancellationToken) is null)
            {
                return;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        throw new InvalidOperationException("The repository still holds the match of a table that was let go of.");
    }

    /// <summary>
    /// A request other than the lobby's sweeps too, once a minute: a host whose lobby nobody opens still lets
    /// go of the tables nobody is at.
    /// </summary>
    [Fact]
    public async Task Any_request_may_sweep_but_only_once_a_minute()
    {
        using var registry = _hosted.Registry();
        var finished = await Composed(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Added(registry, finished);
        var later = finished.CreatedAt + TableRegistry.FinishedFor;

        (await registry.SweepIfDueAsync(later - TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)).ShouldBeEmpty("nothing is old yet");
        (await registry.SweepIfDueAsync(later, TestContext.Current.CancellationToken)).ShouldBeEmpty("the table is old now, but the last sweep was half a minute ago");
        (await registry.SweepIfDueAsync(later + TableRegistry.SweepEvery, TestContext.Current.CancellationToken)).ShouldBe([finished.Id]);
    }

    /// <summary>
    /// A table whose match did not reach an outcome is finished all the same, or it would be neither under
    /// way nor sweepable and stay for the life of the host.
    /// </summary>
    [Fact]
    public async Task A_table_whose_match_was_stopped_is_finished_and_swept_like_any_other()
    {
        using var recording = new HostedTables(recording: true);
        using var registry = recording.Registry();
        var stopped = await Added(registry, await recording.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken));
        stopped.Dispose();
        await Should.ThrowAsync<OperationCanceledException>(() => stopped.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        await Finished(stopped);

        (await registry.SweepAsync(stopped.CreatedAt + TableRegistry.FinishedFor, TestContext.Current.CancellationToken)).ShouldBe([stopped.Id]);
    }

    /// <summary>The closing runs after the outcome, on its own task, so a test waits for it rather than assuming it.</summary>
    private static async Task Finished(PlayedTable table)
    {
        for (var attempt = 0; attempt < 300 && !table.IsFinished; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        table.IsFinished.ShouldBeTrue("the closing should have marked the table finished");
    }

    private Task<PlayedTable> Composed(TableRequest request) => _hosted.Composer.ComposeAsync(request, TestContext.Current.CancellationToken);

    /// <summary>A table into a place reserved for it, as a host adds one: its seats given their codes.</summary>
    private static async Task<PlayedTable> Added(TableRegistry registry, PlayedTable table)
    {
        using var place = (await registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await registry.AddAsync(table, place, TestContext.Current.CancellationToken);
        return table;
    }
}

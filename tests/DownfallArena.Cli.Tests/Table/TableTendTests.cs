using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The hourly job (ADR 0162, phase 6): one pass of the work that is due on the tables, from a process that holds
/// none of them, and no bot turn taken by the tables it picks up.
/// </summary>
public sealed class TableTendTests : IDisposable
{
    private readonly HostedTables _hosted = new(directory => new FileArtifactStore(directory), turns: new NoAgentTurns());

    public void Dispose() => _hosted.Dispose();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_table_that_never_began_is_closed_by_a_pass_a_night_later()
    {
        using var host = _hosted.Registry();
        var table = await RecordedAsync(host, HostedTables.OnePerson);
        using var job = await _hosted.RestartedAsync();

        var tended = await TableTend.TendAsync(job, new LobbyApi(job, _hosted.Composer, TimeProvider.System), table.CreatedAt + TableRegistry.NeverBegunFor + TimeSpan.FromMinutes(1));

        tended.Swept.ShouldBe([table.Id]);
        tended.Expired.ShouldBe(0);
        (await _hosted.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Closed);
    }

    [Fact]
    public async Task A_pass_closes_nothing_that_is_not_due()
    {
        using var host = _hosted.Registry();
        var table = await RecordedAsync(host, HostedTables.OnePerson);
        using var job = await _hosted.RestartedAsync();

        var tended = await TableTend.TendAsync(job, new LobbyApi(job, _hosted.Composer, TimeProvider.System), table.CreatedAt + TimeSpan.FromHours(1));

        tended.Swept.ShouldBeEmpty();
        (await _hosted.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Open);
    }

    [Fact]
    public async Task A_table_the_job_picks_up_takes_no_bot_turn()
    {
        using var host = _hosted.Registry();
        var table = await RecordedAsync(host, HostedTables.Bots);
        using var job = await _hosted.RestartedAsync();

        (await job.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        await Task.Delay(200, Cancel);

        (await _hosted.Composer.JournalAsync(table.Id, Cancel)).ShouldNotBeNull().Entries.ShouldBeEmpty("its bots' turns are the worker's");
    }

    /// <summary>
    /// A game between two players (ADR 0169): one answered, the other let the time to answer run out, and a pass
    /// past the limit ends it with the late one losing, written as any finished game is.
    /// </summary>
    [Fact]
    public async Task A_player_who_let_their_time_to_answer_run_out_loses_at_the_next_pass()
    {
        using var host = _hosted.Registry();
        var table = await BegunAsync(host);
        await PassAsync(table, table.Seats[0]);
        await table.RecordAsync(Cancel);
        using var job = await _hosted.RestartedAsync();

        var tended = await TableTend.TendAsync(job, new LobbyApi(job, _hosted.Composer, TimeProvider.System), DateTimeOffset.UtcNow + TimeSpan.FromHours(25), _hosted.Composer);

        tended.TimedOut.ShouldBe([table.Id]);
        var ended = await job.Held(table.Id).ShouldNotBeNull().Session.Outcome;
        ended.Value.ShouldBe(new Domain.Matches.MatchOutcome(Domain.Matches.PlayerSlot.Player1, Domain.Matches.MatchEndReason.TimedOut));
        (await _hosted.Composer.JournalAsync(table.Id, Cancel)).ShouldNotBeNull().Entries[^1].ShouldBeOfType<TimeoutEntry>().Late.ShouldBe([Domain.Matches.PlayerSlot.Player2]);
    }

    /// <summary>Both players asked at once, and both let the time run out: the game ends drawn (ADR 0169).</summary>
    [Fact]
    public async Task Two_players_asked_at_once_who_both_let_their_time_run_out_draw()
    {
        using var host = _hosted.Registry();
        var table = await BegunAsync(host);
        await table.RecordAsync(Cancel);
        using var job = await _hosted.RestartedAsync();

        var tended = await TableTend.TendAsync(job, new LobbyApi(job, _hosted.Composer, TimeProvider.System), DateTimeOffset.UtcNow + TimeSpan.FromHours(25), _hosted.Composer);

        tended.TimedOut.ShouldBe([table.Id]);
        (await job.Held(table.Id).ShouldNotBeNull().Session.Outcome).Value.ShouldBe(new Domain.Matches.MatchOutcome(null, Domain.Matches.MatchEndReason.TimedOut));
    }

    [Fact]
    public async Task A_game_whose_time_to_answer_has_not_run_out_is_left_as_it_is()
    {
        using var host = _hosted.Registry();
        var table = await BegunAsync(host);
        await table.RecordAsync(Cancel);
        using var job = await _hosted.RestartedAsync();

        var tended = await TableTend.TendAsync(job, new LobbyApi(job, _hosted.Composer, TimeProvider.System), DateTimeOffset.UtcNow + TimeSpan.FromHours(23), _hosted.Composer);

        tended.TimedOut.ShouldBeEmpty();
        (await _hosted.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Open);
    }

    /// <summary>A game between two people with a day to answer, begun: both people reached their seats and are asked their first pick.</summary>
    private async Task<PlayedTable> BegunAsync(TableRegistry registry)
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.TwoPeople, Cancel);
        table.TurnLimit = TimeSpan.FromHours(24);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        foreach (var seat in table.Seats)
        {
            seat.Person!.Arrive();
        }

        table.Session.Begin();
        foreach (var seat in table.Seats)
        {
            for (var attempt = 0; attempt < 300 && seat.Person!.Waiting is null; attempt++)
            {
                await Task.Delay(20, Cancel);
            }
        }

        return table;
    }

    private static async Task PassAsync(PlayedTable table, TableSeat seat)
    {
        var asked = seat.Person!.Waiting.ShouldNotBeNull().Asked;
        var pass = System.Text.Json.JsonSerializer.Serialize(new { kind = "Evolution", pass = true, asked });
        (await table.Api.HandleAsync("POST", $"/api/seat/{seat.Name}/decision", pass, seat.Token)).Status.ShouldBe(204);
    }

    [Fact]
    public async Task The_command_tends_a_store_and_says_what_it_did()
    {
        // Services built for this very command, as the command line builds them: what it needs is registered for it.
        var options = CliOptions.Parse([CliOptions.TendCommand, "--record", _hosted.RunsDirectory]) with { SchemaPath = _hosted.SchemaPath };
        using var services = CliHost.Build(options, seed: 7, logMatchToConsole: false);
        var said = new StringWriter();
        var before = Console.Out;
        Console.SetOut(said);
        int exit;
        try
        {
            exit = await TableTend.RunAsync(services.Services, options, HostedTables.Rules);
        }
        finally
        {
            Console.SetOut(before);
        }

        exit.ShouldBe(0);
        said.ToString().ShouldContain("Tended");
    }

    private async Task<PlayedTable> RecordedAsync(TableRegistry registry, TableRequest request)
    {
        var table = await _hosted.Composer.ComposeAsync(request, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        await table.RecordAsync(Cancel);
        return table;
    }
}

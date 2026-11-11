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

    [Fact]
    public async Task The_command_tends_a_store_and_says_what_it_did()
    {
        var options = CliOptions.Parse([CliOptions.TendCommand, "--record", _hosted.RunsDirectory]);
        var said = new StringWriter();
        var before = Console.Out;
        Console.SetOut(said);
        int exit;
        try
        {
            exit = await TableTend.RunAsync(_hosted.Services, options, HostedTables.Rules);
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

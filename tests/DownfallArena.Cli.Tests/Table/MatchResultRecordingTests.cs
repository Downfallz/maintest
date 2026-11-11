using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Agents;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>A finished table with an account in it leaves a result for a ladder (ADR 0122, stage 5).</summary>
public sealed class MatchResultRecordingTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly Kept _results = new();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private TableComposer Composer(IMatchResults? results = null) =>
        new(_hosted.Services, HostedTables.Rules, new FileArtifactStore(_hosted.RunsDirectory)) { Results = results ?? _results, Index = _hosted.Index };

    /// <summary>A person's seat its understudy plays to the end: a seat with a person in it that finishes on its own.</summary>
    private static TableRequest PlayedByTheUnderstudy => new(null, AgentSpec.Parse("greedy"), Who: null, Handover: 999, Seed: 7);

    private static async Task FinishedAsync(PlayedTable table)
    {
        // A table with a person in it begins once every person has reached their seat (ADR 0092).
        table.Seats[0].Person!.Arrive();
        table.Session.Begin();
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(60), Cancel);
        for (var attempt = 0; attempt < 500 && !table.IsFinished; attempt++)
        {
            await Task.Delay(20, Cancel);
        }
    }

    [Fact]
    public async Task A_finished_table_with_an_account_records_who_sat_where_and_who_won()
    {
        var account = AccountId.New();
        var table = await Composer().ComposeAsync(PlayedByTheUnderstudy, Cancel);
        table.Seats[0].Account = account;

        await FinishedAsync(table);

        var result = _results.All.ShouldHaveSingleItem();
        result.MatchId.ShouldBe(table.Session.MatchId.ToString());
        result.Seats[0].ShouldBe(new ResultSeat("player1", account, null));
        result.Seats[1].Account.ShouldBeNull();
        result.Seats[1].Agent.ShouldNotBeNull().ShouldContain("reedy");
        result.Rounds.ShouldBeGreaterThan(0);
        var outcome = (await table.Session.Outcome).Value;
        result.Winner.ShouldBe(outcome.Winner is { } winner ? TableSeat.NameOf(winner) : null);
        result.Ranked.ShouldBeFalse();
        table.Dispose();
    }

    [Fact]
    public async Task A_table_opened_as_unranked_records_its_result_as_casual()
    {
        var table = await Composer().ComposeAsync(PlayedByTheUnderstudy, Cancel);
        table.Seats[0].Account = AccountId.New();
        table.Casual = true;

        await FinishedAsync(table);

        _results.All.ShouldHaveSingleItem().Casual.ShouldBeTrue();
        table.Dispose();
    }

    [Fact]
    public async Task A_finished_table_nobody_with_an_account_played_records_nothing()
    {
        var table = await Composer().ComposeAsync(PlayedByTheUnderstudy, Cancel);

        await FinishedAsync(table);

        _results.All.ShouldBeEmpty();
        table.Dispose();
    }

    [Fact]
    public async Task A_result_that_could_not_be_kept_leaves_the_table_open_for_the_next_host_to_finish()
    {
        var table = await Composer(new Failing()).ComposeAsync(PlayedByTheUnderstudy, Cancel);
        table.Seats[0].Account = AccountId.New();
        using var registry = _hosted.Registry();
        await AddedAsync(registry, table);
        await table.RecordAsync(Cancel);

        await FinishedAsync(table);

        var record = (await new StoredSessions(new FileArtifactStore(_hosted.RunsDirectory)).RecordAsync(table.Id, Cancel)).ShouldNotBeNull();
        record.Status.ShouldBe(TableRecord.Open);
        table.Dispose();
    }

    [Fact]
    public async Task Deleting_an_account_leaves_a_finished_table_naming_the_account_it_was_played_under()
    {
        var account = AccountId.New();
        var composer = Composer();
        var table = await composer.ComposeAsync(PlayedByTheUnderstudy, Cancel);
        table.Seats[0].Account = account;
        await FinishedAsync(table);
        using var registry = _hosted.Registry();
        await AddedAsync(registry, table);

        var offered = await new LobbyApi(registry, composer, TimeProvider.System).ReleaseAsync(account);

        offered.ShouldBe(0);
        table.Seats[0].Account.ShouldBe(account);
    }

    private static async Task AddedAsync(TableRegistry registry, PlayedTable table)
    {
        using var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull();
        await registry.AddAsync(table, place, Cancel);
    }

    public void Dispose() => _hosted.Dispose();

    private sealed class Failing : IMatchResults
    {
        public Task RecordAsync(MatchResult result, CancellationToken cancellationToken = default) => throw new IOException("The results store is out.");

        public Task<IReadOnlyList<MatchResult>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MatchResult>>([]);
    }

    private sealed class Kept : IMatchResults
    {
        private readonly List<MatchResult> _kept = [];

        public IReadOnlyList<MatchResult> All
        {
            get
            {
                lock (_kept)
                {
                    return [.. _kept];
                }
            }
        }

        public Task RecordAsync(MatchResult result, CancellationToken cancellationToken = default)
        {
            lock (_kept)
            {
                _kept.Add(result);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MatchResult>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult(All);
    }
}

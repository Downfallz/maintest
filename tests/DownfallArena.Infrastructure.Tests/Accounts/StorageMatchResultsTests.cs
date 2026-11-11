using System.Net.Sockets;
using Azure.Data.Tables;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>
/// Results in Table Storage (ADR 0168), against the storage emulator's table service (Azurite), which CI runs and a
/// developer runs with <c>npx azurite-table</c>; skipped without one. Each test has a table of its own.
/// </summary>
public sealed class StorageMatchResultsTests : MatchResultsContract, IDisposable
{
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private static readonly Uri Endpoint = new(Environment.GetEnvironmentVariable("AZURITE_TABLE_ENDPOINT") ?? "http://127.0.0.1:10002/devstoreaccount1");

    private readonly string _before = Path.Combine(Path.GetTempPath(), $"downfall-results-{Guid.NewGuid():N}");
    private readonly TableClient _table = new(Endpoint, $"tests{Guid.NewGuid():N}", new TableSharedKeyCredential(Account, Key));

    public void Dispose()
    {
        if (Directory.Exists(_before))
        {
            Directory.Delete(_before, recursive: true);
        }
    }

    protected override async Task<IMatchResults> ResultsAsync()
    {
        await RequireEmulatorAsync();
        return await StorageMatchResults.OpenAsync(_table, cancellationToken: Cancel);
    }

    [Fact]
    public async Task The_first_opening_takes_in_the_results_kept_before_it_by_match_and_by_account()
    {
        await RequireEmulatorAsync();
        var me = AccountId.New();
        var before = new FileMatchResults(_before);
        var first = Finished(new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.Zero)) with { Seats = [new ResultSeat("player1", me, null), new ResultSeat("player2", null, "greedy")] };
        var second = Finished(new DateTimeOffset(2026, 11, 1, 20, 0, 0, TimeSpan.Zero));
        await before.RecordAsync(first, Cancel);
        await before.RecordAsync(second, Cancel);

        var results = await StorageMatchResults.OpenAsync(_table, before, Cancel);

        (await results.ListAsync(Cancel)).Select(result => result.MatchId).ShouldBe([first.MatchId, second.MatchId]);
        var mine = (await results.ListForAsync(me, Cancel)).ShouldHaveSingleItem();
        mine.MatchId.ShouldBe(first.MatchId);
        mine.Seats.ShouldBe(first.Seats);
    }

    [Fact]
    public async Task A_later_opening_copies_nothing_again()
    {
        await RequireEmulatorAsync();
        var before = new FileMatchResults(_before);
        await before.RecordAsync(Finished(new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.Zero)), Cancel);
        await StorageMatchResults.OpenAsync(_table, before, Cancel);
        await before.RecordAsync(Finished(new DateTimeOffset(2026, 10, 2, 20, 0, 0, TimeSpan.Zero)), Cancel);

        var reopened = await StorageMatchResults.OpenAsync(_table, before, Cancel);

        (await reopened.ListAsync(Cancel)).ShouldHaveSingleItem();
    }

    private static async Task RequireEmulatorAsync()
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(Endpoint.Host, Endpoint.Port, Cancel);
        }
        catch (SocketException)
        {
            Assert.Skip($"No storage emulator's table service at {Endpoint}: run `npx azurite-table` or set AZURITE_TABLE_ENDPOINT.");
        }
    }
}

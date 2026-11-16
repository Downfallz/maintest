using System.Net.Sockets;
using System.Text.Json;
using Azure.Storage;
using Azure.Storage.Blobs;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A seat's last visit (ADR 0194) kept as a blob beside the table's run, against the storage emulator (Azurite).
/// Without one these tests are skipped rather than failed, and say so.
/// </summary>
public sealed class TableVisitsBlobTests
{
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private static readonly Uri Endpoint = new(Environment.GetEnvironmentVariable("AZURITE_BLOB_ENDPOINT") ?? "http://127.0.0.1:10000/devstoreaccount1");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_seats_last_visit_is_a_blob_beside_the_run_written_only_further()
    {
        var store = await StoreAsync();
        using var hosted = new HostedTables(_ => store, turns: new NoAgentTurns());
        using var registry = hosted.Registry();
        var table = await hosted.Composer.ComposeAsync(HostedTables.TwoPeople, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        var seat = table.Seats[0];
        (await table.Api.HandleAsync("PUT", $"/api/seat/{seat.Name}/seen", """{ "sequence": 9 }""", seat.Token)).Status.ShouldBe(200);
        (await table.Api.HandleAsync("PUT", $"/api/seat/{seat.Name}/seen", """{ "sequence": 2 }""", seat.Token)).Status.ShouldBe(200);

        var text = (await store.Reader(table.Run!.SessionId).ReadTextAsync("seen-player1.json", Cancel)).ShouldNotBeNull();
        using var kept = JsonDocument.Parse(text);
        kept.RootElement.GetProperty("sequence").GetInt64().ShouldBe(9);
        (await store.Reader(table.Run.SessionId).ReadTextAsync("seen-player2.json", Cancel)).ShouldBeNull();
        await table.QuiesceAsync(Cancel);
    }

    private static async Task<BlobArtifactStore> StoreAsync()
    {
        if (!await ReachableAsync())
        {
            Assert.Skip($"No storage emulator at {Endpoint}: run `npx azurite-blob` or set AZURITE_BLOB_ENDPOINT.");
        }

        var container = new BlobContainerClient(new Uri($"{Endpoint}/tests-{Guid.CreateVersion7():N}"), new StorageSharedKeyCredential(Account, Key));
        return await BlobArtifactStore.OpenAsync(container, Cancel);
    }

    private static async Task<bool> ReachableAsync()
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(Endpoint.Host, Endpoint.Port, Cancel);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}

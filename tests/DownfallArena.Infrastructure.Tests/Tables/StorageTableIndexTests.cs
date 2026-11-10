using System.Net.Sockets;
using Azure.Data.Tables;
using DownfallArena.Application.Tables.Ports;
using DownfallArena.Infrastructure.Tables;

namespace DownfallArena.Infrastructure.Tests.Tables;

/// <summary>
/// The index of the hosted table, against the storage emulator's table service (Azurite), which CI runs and a
/// developer runs with <c>npx azurite-table</c>. Each test has a table of its own.
/// </summary>
public sealed class StorageTableIndexTests : TableIndexContract
{
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private static readonly Uri Endpoint = new(Environment.GetEnvironmentVariable("AZURITE_TABLE_ENDPOINT") ?? "http://127.0.0.1:10002/devstoreaccount1");

    protected override async Task<ITableIndex> IndexAsync()
    {
        if (!await Reachable())
        {
            Assert.Skip($"No storage emulator's table service at {Endpoint}: run `npx azurite-table` or set AZURITE_TABLE_ENDPOINT.");
        }

        var table = new TableClient(Endpoint, $"tests{Guid.NewGuid():N}", new TableSharedKeyCredential(Account, Key));
        return await StorageTableIndex.OpenAsync(table, TestContext.Current.CancellationToken);
    }

    private static async Task<bool> Reachable()
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(Endpoint.Host, Endpoint.Port, TestContext.Current.CancellationToken);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}

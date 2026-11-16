using System.Net.Sockets;
using Azure.Storage;
using Azure.Storage.Queues;
using DownfallArena.Application.Ports;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Infrastructure.Tests.Work;

/// <summary>
/// The work queues of the hosted table, against the storage emulator's queue service (Azurite), which CI runs and
/// a developer runs with <c>npx azurite-queue</c>. Each test has a queue of its own.
/// </summary>
public sealed class StorageWorkChannelTests : WorkChannelContract
{
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private static readonly Uri Endpoint = new(Environment.GetEnvironmentVariable("AZURITE_QUEUE_ENDPOINT") ?? "http://127.0.0.1:10001/devstoreaccount1");

    protected override async Task<IWorkChannel> ChannelAsync()
    {
        if (!await Reachable())
        {
            Assert.Skip($"No storage emulator's queue service at {Endpoint}: run `npx azurite-queue` or set AZURITE_QUEUE_ENDPOINT.");
        }

        var service = new QueueServiceClient(Endpoint, new StorageSharedKeyCredential(Account, Key));
        return await new StorageWorkChannels(service).OpenAsync($"tests-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);
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

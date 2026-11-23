using System.Net.Sockets;
using Azure.Storage;
using Azure.Storage.Queues;
using DownfallArena.Application.Ports;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>The notices worker over a queue held in this process: what a host on a directory runs.</summary>
public sealed class NoticeWorkerTests : NoticeWorkerContract
{
    private readonly InMemoryWorkChannels _channels = new(TimeProvider.System);

    protected override Task<IWorkChannel> ChannelAsync(string name) => _channels.OpenAsync(name, TestContext.Current.CancellationToken);
}

/// <summary>
/// The notices worker over the storage emulator's queues (Azurite), as the hosted table runs it. Each test has queues of
/// its own; skipped, and said so, without the emulator.
/// </summary>
public sealed class StorageNoticeWorkerTests : NoticeWorkerContract
{
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private static readonly Uri Endpoint = new(Environment.GetEnvironmentVariable("AZURITE_QUEUE_ENDPOINT") ?? "http://127.0.0.1:10001/devstoreaccount1");

    private readonly string _prefix = $"tests-{Guid.NewGuid():N}"[..20];

    protected override async Task<IWorkChannel> ChannelAsync(string name)
    {
        if (!await Reachable())
        {
            Assert.Skip($"No storage emulator's queue service at {Endpoint}: run `npx azurite-queue` or set AZURITE_QUEUE_ENDPOINT.");
        }

        var service = new QueueServiceClient(Endpoint, new StorageSharedKeyCredential(Account, Key));
        return await new StorageWorkChannels(service).OpenAsync($"{_prefix}-{name}", TestContext.Current.CancellationToken);
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

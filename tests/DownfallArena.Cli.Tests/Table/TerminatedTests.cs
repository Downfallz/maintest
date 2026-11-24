using System.Net;
using System.Net.Sockets;
using System.Text;
using Azure.Storage;
using Azure.Storage.Blobs;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The table host and table-worker stopped as the platform stops a replica it scales in, or to zero: SIGTERM, sent to
/// a process of their own, which is the only way production stops them (#1171, #1205). Each reports what it held and
/// exits 0. A process that did not register for the signal is killed mid-work, and exits 143.
/// </summary>
/// <remarks>
/// Not run beside the others: table-worker takes from the emulator account's one <c>agent-turns</c> queue, whose items
/// are the other tests' (#1181). Another run's tests on the same emulator, another session's on the same machine,
/// are not kept from it: their queue items can be taken while it runs.
/// </remarks>
[Collection(Terminated.Alone)]
public sealed class TerminatedTests : IDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly HostedTables _content = new();
    private readonly string _directory = Directory.CreateTempSubdirectory("terminated-").FullName;

    public void Dispose()
    {
        _content.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task On_SIGTERM_the_table_host_says_its_sign_ins_and_exits_0()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "SIGTERM is a POSIX signal.");
        var port = FreePort();
        await using var host = ChildCli.Start("table", "--lobby", "--accounts", "dev", "--port", $"{port}", "--schema", _content.SchemaPath, "--record", Path.Combine(_directory, "runs"));
        await host.LineAsync(line => line.StartsWith("  Admin: ", StringComparison.Ordinal), Cancel);
        using (var client = new HttpClient())
        {
            using var body = new StringContent("""{"username":"MarkF"}""", Encoding.UTF8, "application/json");
            (await client.PostAsync(new Uri($"http://127.0.0.1:{port}/api/account/dev"), body, Cancel)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var exit = await host.TerminateAsync(Cancel);

        exit.ShouldBe(0, string.Join(Environment.NewLine, host.Lines));
        host.Lines.ShouldContain(line => line.StartsWith("  Sign-ins (dev): 1 started, 1 finished,", StringComparison.Ordinal), "the counts held until then are said rather than cut off with the process");
    }

    [Fact]
    public async Task On_SIGTERM_table_worker_stops_and_exits_0()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "SIGTERM is a POSIX signal.");
        if (!await EmulatorAsync())
        {
            Assert.Skip($"No storage emulator at {Emulator}: run `npx azurite` or set AZURITE_BLOB_ENDPOINT.");
        }

        var container = new BlobContainerClient(new Uri($"{Emulator.ToString().TrimEnd('/')}/terminated-{Guid.NewGuid():N}"), new StorageSharedKeyCredential(EmulatorAccount, EmulatorKey));
        try
        {
            await using var worker = ChildCli.Start("table-worker", "--schema", _content.SchemaPath, "--record", container.Uri.ToString());
            await worker.LineAsync(line => line.StartsWith("  Turns: through the queue ", StringComparison.Ordinal), Cancel);

            var exit = await worker.TerminateAsync(Cancel);

            exit.ShouldBe(0, string.Join(Environment.NewLine, worker.Lines));
        }
        finally
        {
            await container.DeleteIfExistsAsync(cancellationToken: CancellationToken.None);
        }
    }

    private const string EmulatorAccount = "devstoreaccount1";

    private const string EmulatorKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    /// <summary>The storage emulator's blob service, where the worker's container is.</summary>
    private static Uri Emulator { get; } = new(Environment.GetEnvironmentVariable("AZURITE_BLOB_ENDPOINT") ?? "http://127.0.0.1:10000/devstoreaccount1");

    /// <summary>Whether the emulator's blob and queue services both answer: the worker opens its queues beside its container.</summary>
    private static async Task<bool> EmulatorAsync()
    {
        foreach (var port in new[] { Emulator.Port, Emulator.Port + 1 })
        {
            using var client = new TcpClient();
            try
            {
                await client.ConnectAsync(Emulator.Host, port, Cancel);
            }
            catch (SocketException)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A port nothing else on this machine holds right now, asked of the machine rather than guessed.</summary>
    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}

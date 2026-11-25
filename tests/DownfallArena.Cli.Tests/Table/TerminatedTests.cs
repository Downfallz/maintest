using System.Net;
using System.Net.Sockets;
using System.Text;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Work;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The table host and table-worker stopped as the platform stops a replica it scales in, or to zero: SIGTERM, sent to
/// a process of their own, which is the only way production stops them (#1171, #1205). Each reports what it held and
/// exits 0. A process that did not register for the signal is killed mid-work, and exits 143.
/// </summary>
/// <remarks>
/// Not run beside the others, which start processes of their own. Its table-worker takes from a turns queue of its own
/// (<see cref="TableHost.TurnsQueueEnvironment" />), deleted after it, never the emulator account's <c>agent-turns</c>,
/// whose items are the other tests' and another run's on the same emulator (#1181, #1259).
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

        var credential = new StorageSharedKeyCredential(EmulatorAccount, EmulatorKey);
        var container = new BlobContainerClient(new Uri($"{Emulator.ToString().TrimEnd('/')}/terminated-{Guid.NewGuid():N}"), credential);
        var queue = $"{AgentTurnWorker.Queue}-{Guid.NewGuid():N}"[..32];
        var queues = new QueueServiceClient(new UriBuilder(Emulator) { Port = Emulator.Port + 1 }.Uri, credential);
        try
        {
            await using var worker = ChildCli.Start(new Dictionary<string, string> { [TableHost.TurnsQueueEnvironment] = queue }, "table-worker", "--schema", _content.SchemaPath, "--record", container.Uri.ToString());
            var said = await worker.LineAsync(line => line.StartsWith("  Turns: through the queue ", StringComparison.Ordinal), Cancel);

            var exit = await worker.TerminateAsync(Cancel);

            said.ShouldStartWith($"  Turns: through the queue {queue},", customMessage: "the worker takes from its own queue, not the emulator's agent-turns");
            exit.ShouldBe(0, string.Join(Environment.NewLine, worker.Lines));
        }
        finally
        {
            await container.DeleteIfExistsAsync(cancellationToken: CancellationToken.None);
            foreach (var name in new[] { queue, PoisonedWork.QueueOf(queue), DeadLetter.QueueOf(queue) })
            {
                await queues.GetQueueClient(name).DeleteIfExistsAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>The host puts its turns on the queue the variable names, the one table-worker takes from (#1259).</summary>
    [Fact]
    public async Task A_host_whose_turns_go_to_the_worker_puts_them_on_the_queue_the_variable_names()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "SIGTERM is a POSIX signal.");
        if (!await EmulatorAsync())
        {
            Assert.Skip($"No storage emulator at {Emulator}: run `npx azurite` or set AZURITE_BLOB_ENDPOINT.");
        }

        var credential = new StorageSharedKeyCredential(EmulatorAccount, EmulatorKey);
        var container = new BlobContainerClient(new Uri($"{Emulator.ToString().TrimEnd('/')}/terminated-{Guid.NewGuid():N}"), credential);
        var queue = $"{AgentTurnWorker.Queue}-{Guid.NewGuid():N}"[..32];
        var queues = new QueueServiceClient(new UriBuilder(Emulator) { Port = Emulator.Port + 1 }.Uri, credential);
        try
        {
            await using var host = ChildCli.Start(new Dictionary<string, string> { [TableHost.TurnsQueueEnvironment] = queue }, "table", "--lobby", "--turns", "worker", "--port", $"{FreePort()}", "--schema", _content.SchemaPath, "--record", container.Uri.ToString());
            var said = await host.LineAsync(line => line.StartsWith("  Turns: through the queue ", StringComparison.Ordinal), Cancel);

            await host.TerminateAsync(Cancel);

            said.ShouldStartWith($"  Turns: through the queue {queue},", customMessage: "the host puts its turns where the worker takes them");
        }
        finally
        {
            await container.DeleteIfExistsAsync(cancellationToken: CancellationToken.None);
            foreach (var name in new[] { queue, PoisonedWork.QueueOf(queue), DeadLetter.QueueOf(queue) })
            {
                await queues.GetQueueClient(name).DeleteIfExistsAsync(CancellationToken.None);
            }
        }
    }

    [Theory]
    [InlineData("table-worker")]
    [InlineData("table")]
    public async Task A_turns_queue_variable_that_names_no_queue_stops_the_command_with_1_and_says_why(string command)
    {
        await using var started = ChildCli.Start(new Dictionary<string, string> { [TableHost.TurnsQueueEnvironment] = "Agent_Turns" }, command, "--schema", _content.SchemaPath, "--record", "http://127.0.0.1:1/devstoreaccount1/none");

        var exit = await started.ExitedAsync(Cancel);

        exit.ShouldBe(1, string.Join(Environment.NewLine, started.Lines));
        started.Lines.ShouldContain(line => line.StartsWith($"{TableHost.TurnsQueueEnvironment} names no queue", StringComparison.Ordinal));
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

using System.Runtime.InteropServices;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Tables.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The <c>table-worker</c> command (ADR 0162, phase 6): the turn worker and the dead-letter worker of phase 5 in a
/// process of their own, over the store a table host records into, until stopped. What the worker app runs, beside
/// a host told <c>--turns worker</c>. It picks the tables up as a host does, from their records and journals, takes
/// the turns their bots owe, and closes a session whose match its turn ended: the result kept for the ladder, and
/// each seat's account told how it ended. It serves no page, so it tells no seat "your turn": the host that serves
/// the seat's page reads the turn within a second and does.
/// </summary>
internal static class TableWorker
{
    /// <summary>What a worker works over: the store and its index, the queues, and the accounts when there are any.</summary>
    internal sealed record Over(IArtifactStore Store, ITableIndex Index, IWorkChannels Channels)
    {
        public IAccountStore? Accounts { get; init; }

        public IMatchResults? Results { get; init; }

        public IDeviceStore? Devices { get; init; }

        /// <summary>Where the content the tables began on is kept (ADR 0163), or nowhere.</summary>
        public IArtifactStore? Contents { get; init; }
    }

    public static async Task<int> RunAsync(IServiceProvider services, CliOptions options, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        var clock = services.GetRequiredService<TimeProvider>();
        var target = options.Record ?? TableHost.DefaultRunsDirectory;
        if (!ArtifactStores.IsContainerUrl(target))
        {
            // A directory's queues are its own process's: the host's turns would never reach this one.
            await Console.Error.WriteLineAsync($"{CliOptions.WorkerCommand} takes the turns a host puts on the storage account's queue: --record <container-url>, not the directory '{target}'. A host on a directory takes its turns itself (--turns queue).");
            return 1;
        }

        PushKeys? keys;
        try
        {
            keys = PushKeys.Load(options.PushKeys, Environment.GetEnvironmentVariable(PushKeys.Environment));
        }
        catch (Exception failure) when (failure is IOException or InvalidDataException or JsonException)
        {
            await Console.Error.WriteLineAsync($"The push keys could not be read: {failure.Message}");
            return 1;
        }

        using var stopping = new CancellationTokenSource();
        ConsoleCancelEventHandler stop = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopping.Cancel();
        };
        Console.CancelKeyPress += stop;

        // The platform stops a replica it scales in with SIGTERM: the item being taken comes back to the queue
        // rather than being cut off between two writes.
        using var terminated = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            stopping.Cancel();
        });
        using var sender = keys is null ? null : new WebPushSender(keys);
        try
        {
            var over = new Over(
                await ArtifactStores.OpenAsync(target, stopping.Token),
                await ArtifactStores.OpenIndexAsync(target, stopping.Token),
                ArtifactStores.OpenChannels(target, clock))
            {
                Contents = await TableHost.OpenContentsAsync(target, stopping.Token),
            };
            if (options.Accounts != AccountsMode.Off)
            {
                over = over with
                {
                    Accounts = await ArtifactStores.OpenAccountsAsync(target, stopping.Token),
                    Results = await ArtifactStores.OpenResultsAsync(target, stopping.Token),
                    Devices = await ArtifactStores.OpenDevicesAsync(target, stopping.Token),
                };
            }

            Console.WriteLine($"Worker over '{target}'");
            await WorkAsync(services, rules, over, sender, stopping.Token);
            return 0;
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Stopped while opening: whatever was taken comes back to the queue once its time is up.
            return 0;
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            // The platform starts the worker again, and every turn waits on the queue until one is running.
            await Console.Error.WriteLineAsync($"The worker could not open '{target}': {failure.Message}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= stop;
        }
    }

    /// <summary>
    /// The two workers over what is opened, and the watch of the tables they hold, until stopped: a turn owed after
    /// the ones an item takes goes back on the queue, for this worker or another.
    /// </summary>
    internal static async Task WorkAsync(IServiceProvider services, RuleSet rules, Over over, IPushSender? sender, CancellationToken stopping)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(over);
        var clock = services.GetRequiredService<TimeProvider>();
        var turns = await over.Channels.OpenAsync(AgentTurnWorker.Queue, stopping);
        var composer = new TableComposer(services, rules, over.Store)
        {
            Contents = over.Contents,
            Turns = new QueuedAgentTurns(turns, clock) { Said = Console.WriteLine },
            Index = over.Index,
            Agents = SeatableAgents.Read(TableHost.WeightsDirectory),
            Results = over.Results,
            Basis = ReplayBasis.Of(EngineVersion.Current, services.GetRequiredService<IGameResources>(), rules),
        };

        // How a match ended, to each seat's account (ADR 0129, stage 4), when the turn that ended it is taken here.
        if (over is { Accounts: { } accounts, Devices: { } devices } && sender is not null)
        {
            composer.Notices = new AccountNotices(sender, new AccountDevices(devices, clock));
            composer.NameOf = async id => (await accounts.FindAsync(id, stopping))?.Username.Value;
        }

        Console.WriteLine(composer.Notices is null
            ? "  Notices: off. How a match ended reaches nobody's devices from here (--accounts and --push-keys)."
            : "  Notices: on. Each seat's account is told how a match ended when its last turn is taken here.");
        using var registry = new TableRegistry(over.Index) { Loader = new TableLoader(new StoredSessions(over.Store), composer, clock), Basis = composer.Basis };
        using var running = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        try
        {
            var working = await TableHost.StartWorkersAsync(over.Channels, turns, registry, clock, running.Token);

            // What the host writes to the tables this worker holds -- a seat's tap, a closed table, a seat changing
            // hands -- is seen as the host sees the worker's (ADR 0162); and a finished table nobody is at any more is
            // let go of, as a host lets go of it, or a worker that never stops would hold every table it ever took.
            var watching = new TableWatch(registry, clock).RunAsync(running.Token);
            var letting = LetGoOfIdleAsync(registry, clock, running.Token);
            await working;
            await watching;
            await letting;
        }
        finally
        {
            // A worker that stopped on its own stops the rest before the tables are let go of under them.
            await running.CancelAsync();
        }
    }

    /// <summary>Lets go of the tables nobody is at any more, every <see cref="TableRegistry.SweepEvery" />, until stopped.</summary>
    private static async Task LetGoOfIdleAsync(TableRegistry registry, TimeProvider clock, CancellationToken stopping)
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                await Task.Delay(TableRegistry.SweepEvery, clock, stopping);
                registry.LetGoOfIdle(clock.GetUtcNow());
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Stopped.
        }
    }
}

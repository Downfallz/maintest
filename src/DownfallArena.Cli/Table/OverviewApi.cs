using System.Text.Json;
using DownfallArena.Application.Accounts.Players;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Admin;
using DownfallArena.Application.Tables.Tending;
using DownfallArena.Application.Work;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Headquarters' Overview (admin plan, A5): the numbers at a glance, each from the source its own tab reads -- the
/// players, the games under way and finished, what waits on players, the jobs, and registration. A source this host
/// does not have is none (null); one that does not answer is <c>{"unavailable":true}</c>, an empty card rather than no
/// overview.
/// </summary>
internal sealed class OverviewApi(TableRegistry registry, TableComposer composer, TimeProvider clock)
{
    public const string Route = "/api/admin/overview";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>The players, on a host that signs them in.</summary>
    public PlayerDirectory? Players { get; init; }

    /// <summary>The results kept for accounts (ADR 0168), on a host that keeps them.</summary>
    public IMatchResults? Results { get; init; }

    /// <summary>The tending runs (A3), on a host that records.</summary>
    public TendRuns? Tending { get; init; }

    /// <summary>The bots' turn queues (A3b), on a host whose turns go through them.</summary>
    public TurnQueues? Turns { get; init; }

    /// <summary>The host's settings (ADR 0177), on a host that signs players in.</summary>
    public HostSettingsCache? Settings { get; init; }

    public static bool Names(string path) => path == Route;

    public async Task<StudioResponse> HandleAsync(string method)
    {
        if (method != "GET")
        {
            return StudioResponse.OfJson(new { error = "Overview.NoRoute", message = $"No such route: {method} {Route}" }, Web, status: 405);
        }

        var now = clock.GetUtcNow();
        var tables = await TryAsync(async () => (object)await TablesAsync(now)) as TableNumbers;
        return StudioResponse.OfJson(
            new
            {
                players = Players is { } players ? await TryAsync(async () => PlayerCounts.Of(await players.ListAsync(), now)) : null,
                games = new
                {
                    inProgress = tables?.InProgress ?? Unavailable,
                    finished = Results is { } results ? await TryAsync(async () => FinishedCounts.Of(await results.ListAsync(), now)) : null,
                },
                waiting = tables?.Waiting ?? Unavailable,
                jobs = new
                {
                    lastRun = Tending is { } tending ? await TryAsync(async () => Run(await tending.LatestAsync())) : null,
                    turns = Turns is { } turns ? await TryAsync(async () => Queues(await turns.StatesAsync())) : null,
                },
                registration = Settings is { } settings ? new { open = (await settings.CurrentAsync()).RegistrationOpen } : null,
            },
            Web);
    }

    /// <summary>What the open tables say: the begun games, against players and against bots, and what waits on a player.</summary>
    private async Task<TableNumbers> TablesAsync(DateTimeOffset now)
    {
        var open = await registry.OpenAsync();
        var begun = open.Where(table => table.Summary.Begun).ToArray();
        var endingSoon = 0;
        foreach (var table in begun.Where(table => table.Summary.TurnLimitHours is not null))
        {
            var last = await composer.LastMoveAsync(table.Id);
            var runsOut = TurnTimeouts.Since(last?.At, table.Summary, table.CreatedAt) + TimeSpan.FromHours(table.Summary.TurnLimitHours!.Value);
            if (runsOut - now <= TimeSpan.FromDays(1))
            {
                endingSoon++;
            }
        }

        return new TableNumbers(
            new { againstPlayers = begun.Count(table => table.Summary.Seats.All(seat => seat.Person)), againstBots = begun.Count(table => table.Summary.Seats.Any(seat => !seat.Person)) },
            new
            {
                openSeats = open.Sum(table => table.Summary.OpenSeatCount),
                invitations = open.Count(table => !table.Summary.Begun && table.Summary.Seats.Any(seat => seat.InvitedAccount is not null && seat.Taken != true)),
                endingSoon,
            });
    }

    // A host that keeps runs and has kept none yet says so, which is neither no job nor a job that does not answer.
    private static object Run(TendRun? run) => run is null
        ? new { id = (string?)null, started = (DateTimeOffset?)null, outcome = "none" }
        : new { id = (string?)run.Id, started = (DateTimeOffset?)run.Started, outcome = run.Outcome == TendOutcome.Failed ? "failed" : "completed" };

    /// <summary>What a card whose source does not answer reads.</summary>
    private static readonly object Unavailable = new { unavailable = true };

    // The queue the worker takes turns from, and the two that hold the turns given up on.
    private static object Queues(IReadOnlyList<QueueState> states) => new
    {
        waiting = states.Where(state => !Failed(state.Name)).Sum(state => state.Count),
        failed = states.Where(state => Failed(state.Name)).Sum(state => state.Count),
    };

    private static bool Failed(string queue) => queue.EndsWith("-poison", StringComparison.Ordinal) || queue.EndsWith("-dlq", StringComparison.Ordinal);

    private static async Task<object> TryAsync(Func<Task<object>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is JsonException)
        {
            Console.WriteLine($"  The overview could not read one of its numbers: {failure.Message}");
            return Unavailable;
        }
    }

    private sealed record TableNumbers(object InProgress, object Waiting);
}

using System.Globalization;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The worker that sends what the notices queue holds (ADR 0203): a notice to one device of an account, and "your
/// turn" once its grace is over. It runs wherever a host's queue is read -- the API, and <c>table-worker</c> (ADR 0162)
/// -- so a notice put on the queue by a process that stops is sent by the next one that looks.
/// </summary>
/// <remarks>
/// It retries as <see cref="Application.Matches.Journaling.AgentTurnWorker" /> does: an item whose push service is out
/// comes back, later each time, and once it has been delivered <see cref="MostDeliveries" /> times it goes on
/// <c>notices-poison</c> with why, for <see cref="DeadLetterWorker" /> to keep on <c>notices-dlq</c>. A device its push
/// service says is gone is forgotten and its endpoint remembered as gone (<see cref="AccountNotices.SendOneAsync" />):
/// that item is done with, not retried.
/// </remarks>
internal sealed class NoticeWorker(IWorkChannel notices, IWorkChannel poison, TimeProvider clock)
{
    /// <summary>How many times an item is delivered before it is given up on, as the turns' own queue.</summary>
    public const int MostDeliveries = 5;

    /// <summary>How many items one look takes: each is one request to a push service, ten seconds at worst.</summary>
    public const int MostAtOnce = 8;

    /// <summary>How long an item is hidden from every other worker while it is sent: far more than one send takes.</summary>
    public static readonly TimeSpan HiddenFor = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The first wait before a failed item comes back, two seconds; each one after waits twice as long, up to a minute.
    /// A test against the storage emulator sets none, so five deliveries take no minute.
    /// </summary>
    public TimeSpan FirstRetry { get; init; } = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan LongestRetry = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FirstIdle = TimeSpan.FromMilliseconds(250);

    // A notice is read by a person waiting on it: an idle queue is looked at every second at most, a few cents a month.
    private static readonly TimeSpan LongestIdle = TimeSpan.FromSeconds(1);

    /// <summary>What sends a notice to one device of an account; none on a host without push keys.</summary>
    public AccountNotices? Devices { get; init; }

    /// <summary>What tells a seat its turn once the grace is over, after it checked the table; none where nobody can.</summary>
    public Func<DueTurn, CancellationToken, Task>? Turns { get; init; }

    /// <summary>What the worker says of an item it sends, retries or gives up on: the host's console.</summary>
    public Action<string> Said { get; init; } = _ => { };

    /// <summary>One look at the queue: up to <see cref="MostAtOnce" /> items sent. How many were received.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var received = await notices.ReceiveAsync(MostAtOnce, HiddenFor, cancellationToken);

        // Side by side: a push service that answers slowly holds its own items, not the others' of the batch.
        await Task.WhenAll(received.Select(work => HandleOrSayAsync(work, cancellationToken)));
        return received.Count;
    }

    // Whatever one item throws, even while it is retried or set aside, ends neither the batch nor the worker: the item
    // comes back once its time is up.
    private async Task HandleOrSayAsync(QueuedWork work, CancellationToken cancellationToken)
    {
        try
        {
            await HandleAsync(work, cancellationToken);
        }
        catch (Exception failure) when (!cancellationToken.IsCancellationRequested)
        {
            Said($"  Notice {work.Id} could not be handled; it comes back: {Hosting.HttpHost.OneLine(failure)}");
        }
    }

    /// <summary>
    /// Looks at the queue until stopped: again at once after a look that found items, and after a wait that grows from a
    /// quarter of a second to one while it finds none. A look that fails is said, and waited out.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var idle = FirstIdle;
        while (!cancellationToken.IsCancellationRequested)
        {
            var found = 0;
            try
            {
                found = await RunOnceAsync(cancellationToken);
            }
            catch (Exception failure) when (!cancellationToken.IsCancellationRequested)
            {
                Said($"  The queue {notices.Name} could not be read: {Hosting.HttpHost.OneLine(failure)}");
            }

            if (found > 0)
            {
                idle = FirstIdle;
                continue;
            }

            try
            {
                await Task.Delay(idle, clock, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            idle = idle + idle < LongestIdle ? idle + idle : LongestIdle;
        }
    }

    private async Task HandleAsync(QueuedWork work, CancellationToken cancellationToken)
    {
        if (QueuedNotice.Parse(work.Body) is not { } notice)
        {
            await PoisonAsync(work, "Not a notice: the item cannot be read as one.", cancellationToken);
            return;
        }

        try
        {
            if (notice.Push is { } push)
            {
                await PushAsync(push, cancellationToken);
            }
            else if (notice.Turn is { } turn)
            {
                await (Turns ?? throw new InvalidOperationException("This process tells no seat its turn."))(turn, cancellationToken);
            }

            await notices.DeleteAsync(work, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopping: the item comes back once its time is up, for whoever looks next.
        }
        catch (Exception failure)
        {
            // A push service that does not answer in time is a TaskCanceledException too: a failure like any other,
            // retried and then given up on, never the end of the worker.
            await FailedAsync(work, Cut($"{failure.GetType().Name}: {Hosting.HttpHost.OneLine(failure)}"), cancellationToken);
        }
    }

    private async Task PushAsync(DevicePush push, CancellationToken cancellationToken)
    {
        var sent = await (Devices ?? throw new InvalidOperationException("This process holds no push keys.")).SendOneAsync(push, cancellationToken);
        Said(sent switch
        {
            DeviceSend.Sent => $"  Account {push.Account}: a {push.Topic} notice reached a device.",
            DeviceSend.Gone => $"  Account {push.Account}: a device is gone from its push service and was forgotten.",
            _ => $"  Account {push.Account}: a {push.Topic} notice was for a device the account no longer holds; dropped.",
        });
    }

    /// <summary>An item whose send failed: tried again later, or given up on once it has been delivered often enough.</summary>
    private async Task FailedAsync(QueuedWork work, string error, CancellationToken cancellationToken)
    {
        if (work.Delivered >= MostDeliveries)
        {
            await PoisonAsync(work, error, cancellationToken);
            return;
        }

        var after = RetryAfter(work.Delivered);
        Said(string.Create(CultureInfo.InvariantCulture, $"  Notice {work.Id} failed on delivery {work.Delivered}; tried again in {after.TotalSeconds:0}s: {error}"));
        await notices.RetryAsync(work, after, cancellationToken);
    }

    /// <summary>The wait before an item delivered so many times comes back: two seconds, then twice as long each time, up to a minute.</summary>
    internal TimeSpan RetryAfter(long delivered)
    {
        var wait = FirstRetry;
        for (var tried = 1; tried < delivered && wait > TimeSpan.Zero && wait < LongestRetry; tried++)
        {
            wait += wait;
        }

        return wait < LongestRetry ? wait : LongestRetry;
    }

    // Kept on the poison queue first, then deleted: a failure between the two leaves the item to come back, not lost.
    // What is set aside says which account and which kind, never the endpoint nor what the notice said (ADR 0203).
    private async Task PoisonAsync(QueuedWork work, string error, CancellationToken cancellationToken)
    {
        var poisoned = new PoisonedWork(notices.Name, SetAside(work.Body), error, work.Delivered, work.QueuedAt, clock.GetUtcNow());
        await poison.SendAsync(poisoned.ToJson(), DeadLettersKeptFor, cancellationToken: cancellationToken);
        await notices.DeleteAsync(work, cancellationToken);
        Said(string.Create(CultureInfo.InvariantCulture, $"  Notice {work.Id} given up on after {work.Delivered} tries, onto {poison.Name}: {error}"));
    }

    /// <summary>How long a notice given up on is kept, on the poison queue and as a dead letter: a week, for an operator to read.</summary>
    public static readonly TimeSpan DeadLettersKeptFor = TimeSpan.FromDays(7);

    /// <summary>What a notice given up on keeps of itself: the account and the topic of a push, the table, seat and asking of a turn.</summary>
    internal static string SetAside(string body) => QueuedNotice.Parse(body) switch
    {
        { Push: { } push } => System.Text.Json.JsonSerializer.Serialize(new { push = new { account = push.Account, topic = push.Topic } }),
        { Turn: { } turn } => QueuedNotice.Of(turn).ToJson(),
        _ => "(not a notice)",
    };

    // A push service's refusal is its own text, and the endpoint may be anybody's: a line, and a short one.
    private static string Cut(string error) => error.Length <= 200 ? error : error[..200] + "…";
}

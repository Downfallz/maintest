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

    /// <summary>
    /// What the worker says of an item it sends, retries or gives up on: the host's console. A push's line starts
    /// <c>Notice sent (kind)</c>, <c>Notice refused (kind)</c> or <c>Notice gone (kind)</c>, which the ops report counts
    /// (<c>scripts/ops/read.sh</c>, N6).
    /// </summary>
    public Action<string> Said { get; init; } = _ => { };

    /// <summary>Where what each look sent, had refused and found gone is added, a kind each (N6); none counts nothing.</summary>
    public NoticeCounts? Counts { get; init; }

    /// <summary>One look at the queue: up to <see cref="MostAtOnce" /> items sent. How many were received.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var received = await notices.ReceiveAsync(MostAtOnce, HiddenFor, cancellationToken);
        var tally = new Tally();

        // Side by side: a push service that answers slowly holds its own items, not the others' of the batch.
        await Task.WhenAll(received.Select(work => HandleOrSayAsync(work, tally, cancellationToken)));
        await KeepAsync(tally, cancellationToken);
        return received.Count;
    }

    // The look's counts, once for the whole batch: a count that cannot be kept is a line, never a notice sent again.
    private async Task KeepAsync(Tally tally, CancellationToken cancellationToken)
    {
        if (Counts is null || tally.Counted() is not { Count: > 0 } counted)
        {
            return;
        }

        try
        {
            if (!await Counts.AddAsync(counted, cancellationToken))
            {
                Said("  The notices' counts of this look were lost: other processes kept writing them first.");
            }
        }
        catch (Exception failure) when (!cancellationToken.IsCancellationRequested)
        {
            Said($"  The notices' counts of this look could not be kept: {Hosting.HttpHost.OneLine(failure)}");
        }
    }

    // Whatever one item throws, even while it is retried or set aside, ends neither the batch nor the worker: the item
    // comes back once its time is up.
    private async Task HandleOrSayAsync(QueuedWork work, Tally tally, CancellationToken cancellationToken)
    {
        try
        {
            await HandleAsync(work, tally, cancellationToken);
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Stopped in the middle of a look, as while idle: what was taken and not deleted comes back to the queue.
                return;
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

    private async Task HandleAsync(QueuedWork work, Tally tally, CancellationToken cancellationToken)
    {
        if (QueuedNotice.Parse(work.Body) is not { } notice)
        {
            await PoisonAsync(work, $"Notice {work.Id}", "Not a notice: the item cannot be read as one.", cancellationToken);
            return;
        }

        try
        {
            if (notice.Push is { } push)
            {
                tally.Pushed(push.Topic, await PushAsync(push, cancellationToken));
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
            // retried and then given up on, never the end of the worker. Only the push service's own is a refusal (N6):
            // the device store or the queue failing around a send is no push service's doing.
            var named = $"Notice {work.Id}";
            var cause = failure;
            if (failure is PushRefusedException { InnerException: { } inner } && notice.Push is { } refused)
            {
                tally.Refused(refused.Topic);
                named = $"Notice refused ({refused.Topic}): {work.Id}";
                cause = inner;
            }

            await FailedAsync(work, named, Cut($"{cause.GetType().Name}: {Hosting.HttpHost.OneLine(cause)}"), cancellationToken);
        }
    }

    private async Task<DeviceSend> PushAsync(DevicePush push, CancellationToken cancellationToken)
    {
        var sent = await (Devices ?? throw new InvalidOperationException("This process holds no push keys.")).SendOneAsync(push, cancellationToken);
        Said(sent switch
        {
            DeviceSend.Sent => $"  Notice sent ({push.Topic}): account {push.Account} reached a device.",
            DeviceSend.Gone => $"  Notice gone ({push.Topic}): account {push.Account} has a device gone from its push service, forgotten.",
            _ => $"  Notice gone ({push.Topic}): account {push.Account} no longer holds the device; dropped.",
        });
        return sent;
    }

    /// <summary>An item whose send failed: tried again later, or given up on once it has been delivered often enough.</summary>
    private async Task FailedAsync(QueuedWork work, string named, string error, CancellationToken cancellationToken)
    {
        if (work.Delivered >= MostDeliveries)
        {
            await PoisonAsync(work, named, error, cancellationToken);
            return;
        }

        var after = RetryAfter(work.Delivered);
        Said(string.Create(CultureInfo.InvariantCulture, $"  {named} failed on delivery {work.Delivered}; tried again in {after.TotalSeconds:0}s: {error}"));
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
    private async Task PoisonAsync(QueuedWork work, string named, string error, CancellationToken cancellationToken)
    {
        var poisoned = new PoisonedWork(notices.Name, SetAside(work.Body), error, work.Delivered, work.QueuedAt, clock.GetUtcNow());
        await poison.SendAsync(poisoned.ToJson(), DeadLettersKeptFor, cancellationToken: cancellationToken);
        await notices.DeleteAsync(work, cancellationToken);
        Said(string.Create(CultureInfo.InvariantCulture, $"  {named} given up on after {work.Delivered} tries, onto {poison.Name}: {error}"));
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

    /// <summary>What one look counted, a kind each, added to by the items of its batch side by side.</summary>
    private sealed class Tally
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, (long Sent, long Refused, long Gone)> _kinds = new(StringComparer.Ordinal);

        /// <summary>A push that went: sent, or gone (a device its push service let go of, or the account did).</summary>
        public void Pushed(string kind, DeviceSend outcome) =>
            Add(kind, outcome == DeviceSend.Sent ? (1, 0, 0) : (0, 0, 1));

        /// <summary>A try its push service refused, or did not answer in time; a store or a queue that failed is not one.</summary>
        public void Refused(string kind) => Add(kind, (0, 1, 0));

        private void Add(string kind, (long Sent, long Refused, long Gone) by)
        {
            lock (_gate)
            {
                var held = _kinds.GetValueOrDefault(kind);
                _kinds[kind] = (held.Sent + by.Sent, held.Refused + by.Refused, held.Gone + by.Gone);
            }
        }

        public IReadOnlyList<NoticeCount> Counted()
        {
            lock (_gate)
            {
                return [.. _kinds.Select(kind => new NoticeCount(kind.Key, kind.Value.Sent, kind.Value.Refused, kind.Value.Gone))];
            }
        }
    }

    // A push service's refusal is its own text, and the endpoint may be anybody's: a line, and a short one.
    private static string Cut(string error) => error.Length <= 200 ? error : error[..200] + "…";
}

using DownfallArena.Application.Ports;

namespace DownfallArena.Infrastructure.Work;

/// <summary>A work queue held in this process, hiding what it hands out as a storage queue does.</summary>
public sealed class InMemoryWorkChannel(string name, TimeProvider clock) : IWorkChannel
{
    /// <summary>How long an item is kept unreceived when the sender says nothing: a storage queue's own week.</summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromDays(7);

    private readonly Lock _gate = new();
    private readonly List<Held> _held = [];

    public string Name => name;

    /// <summary>Every item the queue holds, visible or not: what a test reads a dead-letter queue by.</summary>
    public IReadOnlyList<string> Bodies
    {
        get
        {
            lock (_gate)
            {
                return [.. _held.Select(held => held.Body)];
            }
        }
    }

    public Task SendAsync(string body, TimeSpan? keptFor = null, TimeSpan? hiddenFor = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        var now = clock.GetUtcNow();
        var kept = keptFor ?? KeptFor;
        lock (_gate)
        {
            _held.Add(new Held(Guid.NewGuid().ToString("N"), body, now, kept == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : now + kept)
            {
                VisibleAt = now + (hiddenFor ?? TimeSpan.Zero),
            });
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<QueuedWork>> ReceiveAsync(int most, TimeSpan hiddenFor, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            _held.RemoveAll(held => held.Expires <= now);
            List<QueuedWork> received = [];
            foreach (var held in _held.Where(held => held.VisibleAt <= now).Take(Math.Max(0, most)))
            {
                held.VisibleAt = now + hiddenFor;
                held.Delivered++;
                held.Receipt = Guid.NewGuid().ToString("N");
                received.Add(new QueuedWork(held.Id, held.Receipt, held.Body, held.Delivered, held.QueuedAt));
            }

            return Task.FromResult<IReadOnlyList<QueuedWork>>(received);
        }
    }

    public Task DeleteAsync(QueuedWork work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        lock (_gate)
        {
            _held.RemoveAll(held => held.Id == work.Id && held.Receipt == work.Receipt);
        }

        return Task.CompletedTask;
    }

    public Task RetryAsync(QueuedWork work, TimeSpan after, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            if (_held.Find(held => held.Id == work.Id && held.Receipt == work.Receipt) is { } held)
            {
                held.VisibleAt = now + after;
            }
        }

        return Task.CompletedTask;
    }

    private sealed class Held(string id, string body, DateTimeOffset queuedAt, DateTimeOffset expires)
    {
        public string Id { get; } = id;

        public string Body { get; } = body;

        public DateTimeOffset QueuedAt { get; } = queuedAt;

        public DateTimeOffset Expires { get; } = expires;

        public DateTimeOffset VisibleAt { get; set; } = queuedAt;

        public long Delivered { get; set; }

        public string? Receipt { get; set; }
    }
}

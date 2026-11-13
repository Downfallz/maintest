namespace DownfallArena.Application.Ports;

/// <summary>
/// A queue of work items one worker at a time takes (ADR 0159, phase 5): what a bot's turn to take is handed on
/// by, and where an item that keeps failing goes on its way out. An item received is hidden from every other
/// receiver for as long as it was received for, and comes back, delivered once more, unless it is deleted.
/// Called a channel because the analysis keeps names ending in "Queue" for collections (CA1711).
/// </summary>
public interface IWorkChannel
{
    /// <summary>The queue's name, what a dead letter says it came from.</summary>
    string Name { get; }

    /// <summary>Puts an item on the queue, kept for as long as given, or the queue's own time when none is.</summary>
    /// <param name="keptFor">How long the item is kept unreceived; <see cref="Timeout.InfiniteTimeSpan" /> for ever.</param>
    /// <param name="hiddenFor">How long before the item can first be received; at once when none is given.</param>
    Task SendAsync(string body, TimeSpan? keptFor = null, TimeSpan? hiddenFor = null, CancellationToken cancellationToken = default);

    /// <summary>Up to <paramref name="most" /> items, each hidden from every other receiver for <paramref name="hiddenFor" />.</summary>
    Task<IReadOnlyList<QueuedWork>> ReceiveAsync(int most, TimeSpan hiddenFor, CancellationToken cancellationToken = default);

    /// <summary>The item is done with: it never comes back.</summary>
    Task DeleteAsync(QueuedWork work, CancellationToken cancellationToken = default);

    /// <summary>The item comes back after this long, not before, delivered once more.</summary>
    Task RetryAsync(QueuedWork work, TimeSpan after, CancellationToken cancellationToken = default);

    /// <summary>
    /// Up to <paramref name="most" /> of the items a receiver could take now, first in first, without hiding them or
    /// counting a delivery: what an operator reads a queue by (admin plan, A3b). A storage queue shows its first 32.
    /// </summary>
    Task<IReadOnlyList<PeekedWork>> PeekAsync(int most, CancellationToken cancellationToken = default);

    /// <summary>How many items the queue holds, hidden ones included; a storage queue's count is approximate.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

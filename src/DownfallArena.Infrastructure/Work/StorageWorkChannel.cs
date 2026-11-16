using Azure.Storage.Queues;
using DownfallArena.Application.Ports;

namespace DownfallArena.Infrastructure.Work;

/// <summary>One Azure Storage queue, as a work queue.</summary>
public sealed class StorageWorkChannel(QueueClient queue) : IWorkChannel
{
    /// <summary>The most a storage queue hands out in one receive.</summary>
    public const int MostAtOnce = 32;

    public string Name => queue.Name;

    public async Task SendAsync(string body, TimeSpan? keptFor = null, TimeSpan? hiddenFor = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);

        // A storage queue says "for ever" as a time to live of minus one second.
        var kept = keptFor == Timeout.InfiniteTimeSpan ? TimeSpan.FromSeconds(-1) : keptFor;
        await queue.SendMessageAsync(body, visibilityTimeout: hiddenFor, timeToLive: kept, cancellationToken);
    }

    public async Task<IReadOnlyList<QueuedWork>> ReceiveAsync(int most, TimeSpan hiddenFor, CancellationToken cancellationToken = default)
    {
        var received = await queue.ReceiveMessagesAsync(Math.Clamp(most, 1, MostAtOnce), hiddenFor, cancellationToken);
        return [.. received.Value.Select(message => new QueuedWork(
            message.MessageId,
            message.PopReceipt,
            message.Body.ToString(),
            message.DequeueCount,
            message.InsertedOn ?? DateTimeOffset.MinValue))];
    }

    public async Task DeleteAsync(QueuedWork work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await queue.DeleteMessageAsync(work.Id, work.Receipt, cancellationToken);
    }

    public async Task RetryAsync(QueuedWork work, TimeSpan after, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await queue.UpdateMessageAsync(work.Id, work.Receipt, visibilityTimeout: after, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PeekedWork>> PeekAsync(int most, CancellationToken cancellationToken = default)
    {
        var peeked = await queue.PeekMessagesAsync(Math.Clamp(most, 1, MostAtOnce), cancellationToken);
        return [.. peeked.Value.Select(message => new PeekedWork(
            message.MessageId,
            message.Body.ToString(),
            message.DequeueCount,
            message.InsertedOn ?? DateTimeOffset.MinValue))];
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        (await queue.GetPropertiesAsync(cancellationToken)).Value.ApproximateMessagesCount;
}

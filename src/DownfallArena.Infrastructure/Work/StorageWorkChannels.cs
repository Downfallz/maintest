using System.Collections.Concurrent;
using Azure.Storage.Queues;
using DownfallArena.Application.Ports;

namespace DownfallArena.Infrastructure.Work;

/// <summary>
/// The work queues of a storage account (ADR 0159, phase 5): Azure Storage queues, in the account that holds the
/// runs, reached by the app's identity. Each is created the first time it is opened.
/// </summary>
public sealed class StorageWorkChannels(QueueServiceClient service) : IWorkChannels
{
    private readonly ConcurrentDictionary<string, Task<IWorkChannel>> _opened = new(StringComparer.Ordinal);

    public Task<IWorkChannel> OpenAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _opened.GetOrAdd(name, named => OpenedAsync(named, cancellationToken));
    }

    private async Task<IWorkChannel> OpenedAsync(string name, CancellationToken cancellationToken)
    {
        var queue = service.GetQueueClient(name);
        try
        {
            await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
        catch
        {
            // Not kept: the next open tries again, rather than every caller after a store that was out for a moment.
            _opened.TryRemove(name, out _);
            throw;
        }

        return new StorageWorkChannel(queue);
    }
}

using System.Collections.Concurrent;
using DownfallArena.Application.Ports;

namespace DownfallArena.Infrastructure.Work;

/// <summary>
/// Work queues held in this process: what a host without a storage account's queues runs its workers over, and
/// what a test hands a worker. They go with the process.
/// </summary>
public sealed class InMemoryWorkChannels(TimeProvider clock) : IWorkChannels
{
    private readonly ConcurrentDictionary<string, InMemoryWorkChannel> _queues = new(StringComparer.Ordinal);

    public Task<IWorkChannel> OpenAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Task.FromResult<IWorkChannel>(_queues.GetOrAdd(name, named => new InMemoryWorkChannel(named, clock)));
    }
}

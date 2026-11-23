using System.Globalization;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;

namespace DownfallArena.Infrastructure.Work;

/// <summary>
/// The notices' counts kept in memory (N6), for a test or a host whose queues are its own: each write bumps a version,
/// and a write over an older one is refused, as the file and blob stores refuse it.
/// </summary>
public sealed class InMemoryNoticeCountStore : INoticeCountStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<DateOnly, (IReadOnlyList<NoticeCount> Counts, long Version)> _held = [];
    private long _written;

    public Task<NoticeCountRead> ReadAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_held.TryGetValue(day, out var held)
                ? new NoticeCountRead(held.Counts, held.Version.ToString(CultureInfo.InvariantCulture))
                : new NoticeCountRead([], null));
        }
    }

    public Task<bool> WriteAsync(DateOnly day, IReadOnlyList<NoticeCount> counts, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(counts);
        lock (_gate)
        {
            var now = _held.TryGetValue(day, out var held) ? held.Version.ToString(CultureInfo.InvariantCulture) : null;
            if (now != version)
            {
                return Task.FromResult(false);
            }

            _held[day] = ([.. counts], ++_written);
            return Task.FromResult(true);
        }
    }

    public Task ForgetBeforeAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            foreach (var old in _held.Keys.Where(held => held < day).ToArray())
            {
                _held.Remove(old);
            }
        }

        return Task.CompletedTask;
    }
}

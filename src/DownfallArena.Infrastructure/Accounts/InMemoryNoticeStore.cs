using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Notice logs kept in memory (ADR 0201), for a test or a host that keeps nothing: each write bumps a version, and a
/// write over an older one is refused, as the file and blob stores refuse it.
/// </summary>
/// <param name="clock">What says when a log was written: the system's unless given.</param>
public sealed class InMemoryNoticeStore(TimeProvider? clock = null) : INoticeStore
{
    private readonly Lock _gate = new();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Dictionary<AccountId, (IReadOnlyList<NoticeEntry> Entries, long Version, DateTimeOffset Written)> _held = [];

    // One count for every log, so a log deleted and written again never comes back at a version a reader still holds.
    private long _written;

    public Task<NoticeLogRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_held.TryGetValue(account, out var held)
                ? new NoticeLogRead(held.Entries, held.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)) { Written = held.Written }
                : new NoticeLogRead([], null));
        }
    }

    public Task<bool> WriteAsync(AccountId account, IReadOnlyList<NoticeEntry> entries, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        lock (_gate)
        {
            var now = _held.TryGetValue(account, out var held) ? held.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            if (now != version)
            {
                return Task.FromResult(false);
            }

            if (entries.Count == 0)
            {
                _held.Remove(account);
            }
            else
            {
                _held[account] = ([.. entries], ++_written, _clock.GetUtcNow());
            }

            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<AccountId>> AccountsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<AccountId>>([.. _held.Keys]);
        }
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _held.Remove(account);
        }

        return Task.CompletedTask;
    }
}

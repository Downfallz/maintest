using System.Collections.Concurrent;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// Writes when an account signs in and when it is seen (ADR 0174). A sign-in is written every time; being seen is
/// written at most once an hour an account, so a player in a game costs one write an hour rather than one a request.
/// What this process last wrote is remembered, so a request in between reads nothing either.
/// </summary>
public sealed class AccountActivityLog
{
    /// <summary>The most the last-seen time lags behind, and the least time between two of its writes.</summary>
    public static readonly TimeSpan SeenEvery = TimeSpan.FromHours(1);

    private readonly IAccountActivityStore _store;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<AccountId, DateTimeOffset> _seen = new();

    // A write reads the record, changes one time and writes it back: a sign-in and a request at once would otherwise
    // each write back the other's old time. Writes are rare, so one lock in the process covers them all, as the
    // registry's does: one host runs at most one replica (ADR 0080).
    private static readonly SemaphoreSlim Writes = new(1, 1);

    public AccountActivityLog(IAccountActivityStore store, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _clock = clock;
    }

    /// <summary>The account signed in now: it is also seen now.</summary>
    public Task SignedInAsync(AccountId account, CancellationToken cancellationToken = default) =>
        WriteAsync(account, signedIn: true, cancellationToken);

    /// <summary>A request carried the account's session: written when the time kept is an hour old or more.</summary>
    public Task SeenAsync(AccountId account, CancellationToken cancellationToken = default) =>
        _seen.TryGetValue(account, out var written) && _clock.GetUtcNow() - written < SeenEvery
            ? Task.CompletedTask
            : WriteAsync(account, signedIn: false, cancellationToken);

    public Task<AccountActivity?> FindAsync(AccountId account, CancellationToken cancellationToken = default) =>
        _store.FindAsync(account, cancellationToken);

    public Task<IReadOnlyList<AccountActivity>> ListAsync(CancellationToken cancellationToken = default) =>
        _store.ListAsync(cancellationToken);

    /// <summary>Removes what was kept: the account going.</summary>
    public async Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            _seen.TryRemove(account, out _);
            await _store.ForgetAsync(account, cancellationToken);
        }
        finally
        {
            Writes.Release();
        }
    }

    private async Task WriteAsync(AccountId account, bool signedIn, CancellationToken cancellationToken)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            var now = _clock.GetUtcNow();
            var kept = await _store.FindAsync(account, cancellationToken);
            // Another process, or this one before a restart, may have written it within the hour.
            if (!signedIn && kept?.LastSeen is { } seen && now - seen < SeenEvery)
            {
                _seen[account] = seen;
                return;
            }

            await _store.SaveAsync(new AccountActivity(account, signedIn ? now : kept?.LastSignIn, now), cancellationToken);
            _seen[account] = now;
        }
        finally
        {
            Writes.Release();
        }
    }
}

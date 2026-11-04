using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// The devices an account can be told on (friends plan, stage 1): a browser registers itself, once, by its push
/// endpoint; an account keeps at most <see cref="MostPerAccount" />, the oldest going when a sixth arrives, so a
/// player who changes phones is not told on five dead ones for good. A list is read, changed and written back,
/// so two changes to one account take the one lock the account writes already take: one host runs one replica
/// (ADR 0080).
/// </summary>
public sealed class AccountDevices
{
    public const int MostPerAccount = 5;

    private static readonly SemaphoreSlim Writes = new(1, 1);

    private readonly IDeviceStore _store;
    private readonly TimeProvider _clock;

    public AccountDevices(IDeviceStore store, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _clock = clock;
    }

    public Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default) =>
        _store.ListAsync(account, cancellationToken);

    /// <summary>
    /// Registers the device, stamped now. A device already there by its endpoint is replaced where it stands: a
    /// browser sends its subscription on every page load, and its keys may have been rotated meanwhile. The list
    /// as it is afterwards.
    /// </summary>
    public async Task<IReadOnlyList<PushDevice>> AddAsync(AccountId account, PushDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        var stamped = device with { AddedAt = _clock.GetUtcNow() };
        await Writes.WaitAsync(cancellationToken);
        try
        {
            var held = await _store.ListAsync(account, cancellationToken);
            var kept = held.Where(known => !string.Equals(known.Endpoint, stamped.Endpoint, StringComparison.Ordinal))
                .Append(stamped)
                .OrderBy(known => known.AddedAt)
                .ToList();
            if (kept.Count > MostPerAccount)
            {
                kept.RemoveRange(0, kept.Count - MostPerAccount);
            }

            await _store.SaveAsync(account, kept, cancellationToken);
            return kept;
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>Forgets the device at that endpoint: the browser asked, or its push service said it is gone. False when there was none.</summary>
    public async Task<bool> RemoveAsync(AccountId account, string endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        await Writes.WaitAsync(cancellationToken);
        try
        {
            var held = await _store.ListAsync(account, cancellationToken);
            var kept = held.Where(known => !string.Equals(known.Endpoint, endpoint, StringComparison.Ordinal)).ToList();
            if (kept.Count == held.Count)
            {
                return false;
            }

            await _store.SaveAsync(account, kept, cancellationToken);
            return true;
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>Every device goes: the account signed out everywhere, or is being deleted.</summary>
    public async Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            await _store.ForgetAsync(account, cancellationToken);
        }
        finally
        {
            Writes.Release();
        }
    }
}

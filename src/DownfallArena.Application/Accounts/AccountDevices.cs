using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// The devices an account can be told on (friends plan, stage 1): a browser registers itself, once, by its push
/// endpoint; an account keeps at most <see cref="MostPerAccount" />, the oldest going when a sixth arrives, so a
/// player who changes phones is not told on five dead ones for good. A list is read with its version, changed and
/// written over that version only (ADR 0201): the API registers a device while the worker forgets a dead one, and
/// no lock of one process covers the other.
/// </summary>
public sealed class AccountDevices
{
    public const int MostPerAccount = 5;

    /// <summary>How many writes in a row a change may lose to others before it gives up.</summary>
    public const int Tries = 8;

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
        return (await ChangeAsync(account, held =>
        {
            var kept = held.Where(known => !string.Equals(known.Endpoint, stamped.Endpoint, StringComparison.Ordinal))
                .Append(stamped)
                .OrderBy(known => known.AddedAt)
                .ToList();
            if (kept.Count > MostPerAccount)
            {
                kept.RemoveRange(0, kept.Count - MostPerAccount);
            }

            return kept;
        }, cancellationToken))!;
    }

    /// <summary>Forgets the device at that endpoint: the browser asked, or its push service said it is gone. False when there was none.</summary>
    public async Task<bool> RemoveAsync(AccountId account, string endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        var removed = false;
        await ChangeAsync(account, held =>
        {
            var kept = held.Where(known => !string.Equals(known.Endpoint, endpoint, StringComparison.Ordinal)).ToList();
            removed = kept.Count != held.Count;
            return removed ? kept : null;
        }, cancellationToken);
        return removed;
    }

    /// <summary>Every device goes: the account signed out everywhere, or is being deleted.</summary>
    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) => _store.ForgetAsync(account, cancellationToken);

    /// <summary>
    /// Reads the list, changes it and writes it over the version read, reading again while another write comes first.
    /// A change that answers none writes nothing and answers none. Every try lost, it throws.
    /// </summary>
    private async Task<IReadOnlyList<PushDevice>?> ChangeAsync(AccountId account, Func<IReadOnlyList<PushDevice>, IReadOnlyList<PushDevice>?> change, CancellationToken cancellationToken)
    {
        for (var tried = 0; tried < Tries; tried++)
        {
            var read = await _store.ReadAsync(account, cancellationToken);
            var changed = change(read.Devices);
            if (changed is null || await _store.SaveAsync(account, changed, read.Version, cancellationToken))
            {
                return changed;
            }
        }

        throw new IOException($"The devices of account {account} lost {Tries} writes in a row to others.");
    }
}

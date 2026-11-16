using System.Collections.Concurrent;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// Who may not send feedback now (docs/tabletop/feedback-plan.md, "Against spam"). An account's mute is kept with its
/// activity record (ADR 0174), so it outlives a restart and goes with the account. A guest's is a seat's, counted
/// against a hash of its key, and kept in this process's memory: it dies with its seat, or with the process, as the
/// count of a guest's address does (ADR 0189).
/// </summary>
public sealed class FeedbackMutes(AccountActivityLog? activity)
{
    private readonly ConcurrentDictionary<string, FeedbackMute> _guests = new(StringComparer.Ordinal);

    /// <summary>The mute that holds on the sender at that time, or none.</summary>
    public async Task<FeedbackMute?> FindAsync(string sender, string? account, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var mute = await KeptAsync(sender, account, cancellationToken);
        return mute is not null && mute.HoldsAt(now) ? mute : null;
    }

    /// <summary>Mutes the sender, or lifts its mute when <paramref name="mute" /> is none. Answers whether there was anyone to hold it.</summary>
    public async Task<bool> SetAsync(string sender, string? account, FeedbackMute? mute, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sender);
        if (account is not null)
        {
            return activity is not null && AccountId.TryParse(account, out var id) && await activity.MuteFeedbackAsync(id, mute, cancellationToken);
        }

        if (mute is null)
        {
            _guests.TryRemove(sender, out _);
        }
        else
        {
            _guests[sender] = mute;
        }

        return true;
    }

    private async Task<FeedbackMute?> KeptAsync(string sender, string? account, CancellationToken cancellationToken)
    {
        if (account is null)
        {
            return _guests.TryGetValue(sender, out var guest) ? guest : null;
        }

        return activity is not null && AccountId.TryParse(account, out var id)
            ? (await activity.FindAsync(id, cancellationToken))?.FeedbackMute
            : null;
    }
}

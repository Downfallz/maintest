using System.Collections.Concurrent;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// Who may not send feedback now (docs/tabletop/feedback-plan.md, "Against spam"). An account's mute is kept with its
/// activity record (ADR 0174), so it outlives a restart and goes with the account, and is dropped at the record's first
/// write after it ends. A guest's is a seat's, counted against a hash of its key, and kept in this process's memory: it
/// dies with its seat, or with the process, as the count of a guest's address does (ADR 0189).
/// </summary>
public sealed class FeedbackMutes(AccountActivityLog? activity)
{
    private readonly ConcurrentDictionary<string, FeedbackMute> _guests = new(StringComparer.Ordinal);

    /// <summary>Whether a mute can be kept for the sender: a guest's always, an account's on a host that keeps account activity.</summary>
    public bool CanHold(string? account) => account is null || (activity is not null && AccountId.TryParse(account, out _));

    /// <summary>Whether the feedback's sender can be muted: one is kept for it, and its account was not deleted.</summary>
    public bool CanMute(Feedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        return feedback.Sender != ForgetFeedbackAccount.Gone && CanHold(feedback.Account);
    }

    /// <summary>The mute that holds on the feedback's sender at that time, or none; none for a deleted account's.</summary>
    public Task<FeedbackMute?> OfSenderAsync(Feedback feedback, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        return feedback.Sender == ForgetFeedbackAccount.Gone
            ? Task.FromResult<FeedbackMute?>(null)
            : FindAsync(feedback.Sender, feedback.Account, now, cancellationToken);
    }

    /// <summary>The mute that holds on the sender at that time, or none.</summary>
    public async Task<FeedbackMute?> FindAsync(string sender, string? account, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (account is null)
        {
            if (_guests.TryGetValue(sender, out var guest) && !guest.HoldsAt(now))
            {
                _guests.TryRemove(new KeyValuePair<string, FeedbackMute>(sender, guest));
                return null;
            }

            return guest;
        }

        var mute = activity is not null && AccountId.TryParse(account, out var id)
            ? (await activity.FindAsync(id, cancellationToken))?.FeedbackMute
            : null;
        return mute is not null && mute.HoldsAt(now) ? mute : null;
    }

    /// <summary>Mutes the sender, or lifts its mute when <paramref name="mute" /> is none. Nothing for a sender <see cref="CanHold" /> refuses.</summary>
    public async Task SetAsync(string sender, string? account, FeedbackMute? mute, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sender);
        if (account is not null)
        {
            if (activity is not null && AccountId.TryParse(account, out var id))
            {
                await activity.MuteFeedbackAsync(id, mute, cancellationToken);
            }

            return;
        }

        if (mute is null)
        {
            _guests.TryRemove(sender, out _);
        }
        else
        {
            _guests[sender] = mute;
        }
    }
}

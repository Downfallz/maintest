using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The one door every notice to an account goes through (ADR 0201): first the bell, which keeps a notice that is news
/// in the account's notice log whatever the push does, then the push, to the account's devices, when the host has
/// keys to send one. No send site picks a channel. A log that cannot be written is a line on the console and the push
/// still goes: a notice is never a reason to fail the request that caused it.
/// </summary>
internal sealed class AccountNotifier(AccountNoticeLog? log, AccountNotices? push)
{
    /// <summary>The log the bell reads; none on a host that keeps none.</summary>
    public AccountNoticeLog? Log => log;

    /// <summary>The push channel; none on a host without push keys.</summary>
    public AccountNotices? Push => push;

    /// <summary>What says the account's lists changed, so its bell reads the new entry on no clock (ADR 0164); none where nobody listens.</summary>
    public LobbyChanges? Changes { get; set; }

    /// <summary>
    /// Whether the account is still there: a log is never kept for an account that is gone, and one written while it
    /// was being deleted is forgotten again (the account goes first, then its log). None where every account is kept.
    /// </summary>
    public Func<AccountId, CancellationToken, Task<bool>>? Exists { get; init; }

    /// <summary>
    /// What pops up on the account's devices (ADR 0202): a kind the account turned off is kept in the bell and not
    /// pushed. None where every kind is pushed.
    /// </summary>
    public IPreferenceStore? Preferences { get; init; }

    /// <summary>
    /// The notices queue (ADR 0203): the push goes on it, one item a device, for <see cref="NoticeWorker" /> to send off
    /// the request. None on a host with no queue, which sends in the request, as before.
    /// </summary>
    public IWorkChannel? Outbox { get; set; }

    /// <summary>Where a line about a notice the bell could not keep is said: the console, unless a test listens.</summary>
    public Action<string> Said { get; init; } = Console.WriteLine;

    /// <summary>The bell, then the push. How many devices took the push, or how many it was queued for.</summary>
    public Task<int> NotifyAsync(AccountId to, Notice notice, CancellationToken cancellationToken = default) =>
        NotifyAsync(to, notice, except: null, cancellationToken);

    /// <summary>
    /// The bell, then the push to every device of the account but the endpoints <paramref name="except" /> names: a
    /// phone that holds the other seat of the table is hotseat, and is told nothing (ADR 0120). How many devices took the push.
    /// </summary>
    public async Task<int> NotifyAsync(AccountId to, Notice notice, IReadOnlyCollection<string>? except, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notice);
        if (log is not null && NoticeKinds.IsNews(notice.Kind))
        {
            try
            {
                await KeepAsync(log, to, notice, cancellationToken);
            }
            catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is System.Text.Json.JsonException or InvalidDataException)
            {
                Said($"  Account {to}: a notice could not be kept in its bell: {Hosting.HttpHost.OneLine(failure)}");
            }
        }

        if (push is null || !await PopsAsync(to, notice.Kind, cancellationToken))
        {
            return 0;
        }

        var payload = AccountNotices.Payload(notice.Title, notice.Body, notice.Url, notice.Tag);
        try
        {
            if (Outbox is { } outbox)
            {
                try
                {
                    return await push.EnqueueAsync(outbox, to, payload, TopicOf(notice.Kind), except, cancellationToken);
                }
                catch (Exception failure) when (Unreachable(failure))
                {
                    // A queue that is out is no reason to lose the notice: it goes in the request, as on a host with none.
                    Said($"  Account {to}: a notice could not be queued, so it is sent now: {Hosting.HttpHost.OneLine(failure)}");
                }
            }

            return await push.SendAsync(to, payload, TopicOf(notice.Kind), except, cancellationToken);
        }
        catch (Exception failure) when (Unreachable(failure) || failure is System.Text.Json.JsonException or InvalidDataException)
        {
            // The devices could not be read: what the request did is done, and the notice is a line, never a 500.
            Said($"  Account {to}: a notice could not be pushed: {Hosting.HttpHost.OneLine(failure)}");
            return 0;
        }
    }

    // A choice that cannot be read is no reason to keep a notice from the phone: it goes, as it did before the choice.
    private async Task<bool> PopsAsync(AccountId to, NoticeKind kind, CancellationToken cancellationToken)
    {
        if (Preferences is not { } preferences)
        {
            return true;
        }

        try
        {
            return (await preferences.GetNoticesAsync(to, cancellationToken)).Pops(kind);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure))
        {
            Said($"  Account {to}: what pops up could not be read, so the notice is pushed: {Hosting.HttpHost.OneLine(failure)}");
            return true;
        }
    }

    private async Task KeepAsync(AccountNoticeLog kept, AccountId to, Notice notice, CancellationToken cancellationToken)
    {
        if (Exists is { } exists && !await exists(to, cancellationToken))
        {
            return;
        }

        await kept.AddAsync(to, notice, cancellationToken);
        // Read again after the write: a deletion that forgot the log between the check and the write would leave
        // this one behind for good, with nobody left to forget it.
        if (Exists is { } still && !await still(to, cancellationToken))
        {
            await kept.ForgetAsync(to, cancellationToken);
            return;
        }

        Changes?.Account(to);
    }

    private static bool Unreachable(Exception failure) =>
        TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException or HttpRequestException;

    /// <summary>The push service's topic a kind is sent under: a later notice of one topic replaces an earlier one still queued.</summary>
    internal static string TopicOf(NoticeKind kind) => kind switch
    {
        NoticeKind.Turn => "turn",
        NoticeKind.FriendRequest or NoticeKind.FriendAccepted => "friend",
        NoticeKind.Invitation or NoticeKind.SeatTaken or NoticeKind.Declined => "invitation",
        _ => "match",
    };
}

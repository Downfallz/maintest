using DownfallArena.Application.Accounts;
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

    /// <summary>Where a line about a notice the bell could not keep is said: the console, unless a test listens.</summary>
    public Action<string> Said { get; init; } = Console.WriteLine;

    /// <summary>The bell, then the push. How many devices took the push.</summary>
    public async Task<int> NotifyAsync(AccountId to, Notice notice, CancellationToken cancellationToken = default)
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

        return push is null ? 0 : await push.SendAsync(to, AccountNotices.Payload(notice.Title, notice.Body, notice.Url, notice.Tag), TopicOf(notice.Kind), cancellationToken);
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

    /// <summary>The push service's topic a kind is sent under: a later notice of one topic replaces an earlier one still queued.</summary>
    internal static string TopicOf(NoticeKind kind) => kind switch
    {
        NoticeKind.Turn => "turn",
        NoticeKind.FriendRequest or NoticeKind.FriendAccepted => "friend",
        NoticeKind.Invitation or NoticeKind.SeatTaken or NoticeKind.Declined => "invitation",
        _ => "match",
    };
}

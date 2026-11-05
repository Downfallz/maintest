using DownfallArena.Application.Accounts.Friends;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Where each account's side of its friendships is kept (ADR 0129): one document per account, read and written
/// whole. The rules -- mutual, one request per pair, how many -- are <see cref="Friends" />'s; this only keeps the
/// lists. Owned by Application, implemented by Infrastructure beside the account store.
/// </summary>
public interface IFriendStore
{
    /// <summary>The account's list; <see cref="FriendList.Empty" /> for an account that has none.</summary>
    Task<FriendList> ListAsync(AccountId account, CancellationToken cancellationToken = default);

    /// <summary>Replaces the account's list; an empty one leaves nothing behind.</summary>
    Task SaveAsync(AccountId account, FriendList list, CancellationToken cancellationToken = default);

    /// <summary>Removes the account's own document; the links other accounts hold to it are the caller's to clear.</summary>
    Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default);
}

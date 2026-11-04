using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Accounts.Friends;

/// <summary>
/// Friendships (ADR 0129): one player asks another by their exact name, the other accepts or declines, and until
/// then it is a request on both lists. Asking somebody who already asked you accepts. Declining, withdrawing and
/// removing are silent and leave neither side with a link. Every change writes two documents, the asker's then the
/// other's, under the one lock all account writes take: one host runs one replica (ADR 0080), and a change that
/// stopped between its two writes is put right by the next change between the two.
/// </summary>
public sealed class Friends
{
    /// <summary>How many friends, and how many pending requests, one account keeps at most: a bound on a document, and on a lobby panel.</summary>
    public const int MostLinks = 100;

    private static readonly SemaphoreSlim Writes = new(1, 1);

    private readonly IFriendStore _store;
    private readonly IAccountStore _accounts;
    private readonly TimeProvider _clock;

    public Friends(IFriendStore store, IAccountStore accounts, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _accounts = accounts;
        _clock = clock;
    }

    public Task<FriendList> ListAsync(AccountId account, CancellationToken cancellationToken = default) => _store.ListAsync(account, cancellationToken);

    /// <summary>Whether the two are friends, read from the first one's list.</summary>
    public async Task<bool> AreFriendsAsync(AccountId one, AccountId other, CancellationToken cancellationToken = default) =>
        (await _store.ListAsync(one, cancellationToken)).Has(other);

    /// <summary>
    /// Asks the player with that name to be a friend: a request they will see, or a friendship at once when they
    /// had already asked. Refused for a name nobody holds, for your own, for a friend already, for a player already
    /// asked, and past <see cref="MostLinks" /> on either side.
    /// </summary>
    public async Task<Result<FriendAsked>> AskAsync(AccountId me, string? name, CancellationToken cancellationToken = default)
    {
        if (await ResolveAsync(name, cancellationToken) is not { } other)
        {
            return Result.Failure<FriendAsked>(FriendErrors.NoSuchPlayer);
        }

        if (other == me)
        {
            return Result.Failure<FriendAsked>(FriendErrors.Self);
        }

        await Writes.WaitAsync(cancellationToken);
        try
        {
            var mine = await _store.ListAsync(me, cancellationToken);
            var theirs = await _store.ListAsync(other, cancellationToken);
            if (mine.Has(other) || theirs.Has(me))
            {
                // One side only is a change that stopped halfway: both sides are made to agree.
                await LinkAsync(me, mine, other, theirs, cancellationToken);
                return Result.Failure<FriendAsked>(FriendErrors.AlreadyFriends);
            }

            if (mine.AskedBy(other) || theirs.Asked(me))
            {
                await LinkAsync(me, mine, other, theirs, cancellationToken);
                return Result.Success(new FriendAsked(other, Accepted: true));
            }

            if (mine.Asked(other) && theirs.AskedBy(me))
            {
                return Result.Failure<FriendAsked>(FriendErrors.AlreadyAsked);
            }

            if (mine.Friends.Count + mine.Outgoing.Count >= MostLinks || theirs.Friends.Count + theirs.Incoming.Count >= MostLinks)
            {
                return Result.Failure<FriendAsked>(FriendErrors.TooMany);
            }

            var now = _clock.GetUtcNow();
            await _store.SaveAsync(me, mine.Without(other) with { Outgoing = [.. mine.Outgoing.Where(link => link.Other != other), new FriendLink(other, now)] }, cancellationToken);
            await _store.SaveAsync(other, theirs.Without(me) with { Incoming = [.. theirs.Incoming.Where(link => link.Other != me), new FriendLink(me, now)] }, cancellationToken);
            return Result.Success(new FriendAsked(other, Accepted: false));
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>Accepts the request that account made: friends from now on, on both lists.</summary>
    public async Task<Result> AcceptAsync(AccountId me, AccountId other, CancellationToken cancellationToken = default)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            var mine = await _store.ListAsync(me, cancellationToken);
            var theirs = await _store.ListAsync(other, cancellationToken);
            if (!mine.AskedBy(other) && !theirs.Asked(me))
            {
                return mine.Has(other) ? Result.Failure(FriendErrors.AlreadyFriends) : Result.Failure(FriendErrors.NoSuchRequest);
            }

            await LinkAsync(me, mine, other, theirs, cancellationToken);
            return Result.Success();
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>
    /// Declines that account's request, withdraws yours to it, or removes it as a friend: whichever link there is,
    /// neither side keeps it. True when there was something to remove.
    /// </summary>
    public async Task<bool> UnlinkAsync(AccountId me, AccountId other, CancellationToken cancellationToken = default)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            var mine = await _store.ListAsync(me, cancellationToken);
            var theirs = await _store.ListAsync(other, cancellationToken);
            if (!mine.Links(other) && !theirs.Links(me))
            {
                return false;
            }

            await _store.SaveAsync(me, mine.Without(other), cancellationToken);
            await _store.SaveAsync(other, theirs.Without(me), cancellationToken);
            return true;
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>The account is going (ADR 0122): no list of its own, and no link to it on anybody else's.</summary>
    public async Task ForgetAsync(AccountId me, CancellationToken cancellationToken = default)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            var mine = await _store.ListAsync(me, cancellationToken);
            foreach (var other in mine.Friends.Concat(mine.Incoming).Concat(mine.Outgoing).Select(link => link.Other).Distinct())
            {
                var theirs = await _store.ListAsync(other, cancellationToken);
                await _store.SaveAsync(other, theirs.Without(me), cancellationToken);
            }

            await _store.ForgetAsync(me, cancellationToken);
        }
        finally
        {
            Writes.Release();
        }
    }

    private async Task LinkAsync(AccountId me, FriendList mine, AccountId other, FriendList theirs, CancellationToken cancellationToken)
    {
        var since = mine.Friends.Concat(theirs.Friends).FirstOrDefault(link => link.Other == other || link.Other == me)?.Since ?? _clock.GetUtcNow();
        await _store.SaveAsync(me, mine.Without(other) with { Friends = [.. mine.Friends.Where(link => link.Other != other), new FriendLink(other, since)] }, cancellationToken);
        await _store.SaveAsync(other, theirs.Without(me) with { Friends = [.. theirs.Friends.Where(link => link.Other != me), new FriendLink(me, since)] }, cancellationToken);
    }

    /// <summary>The account with that name, or none: a name nobody holds, a name whose account is gone, or not a name at all.</summary>
    private async Task<AccountId?> ResolveAsync(string? name, CancellationToken cancellationToken)
    {
        var username = Username.Create(name);
        if (username.IsFailure)
        {
            return null;
        }

        if (await _accounts.FindUsernameAsync(username.Value, cancellationToken) is not { } held)
        {
            return null;
        }

        return await _accounts.FindAsync(held.Account, cancellationToken) is null ? null : held.Account;
    }
}

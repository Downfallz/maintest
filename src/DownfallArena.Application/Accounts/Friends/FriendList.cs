using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Friends;

/// <summary>
/// One account's side of its friendships (ADR 0129): who its friends are, who asked it, and whom it asked. Each
/// friendship and each request is written on both accounts' lists, so either side reads its own document alone.
/// </summary>
public sealed record FriendList(IReadOnlyList<FriendLink> Friends, IReadOnlyList<FriendLink> Incoming, IReadOnlyList<FriendLink> Outgoing)
{
    public static readonly FriendList Empty = new([], [], []);

    public bool Has(AccountId other) => Friends.Any(link => link.Other == other);

    public bool Asked(AccountId other) => Outgoing.Any(link => link.Other == other);

    public bool AskedBy(AccountId other) => Incoming.Any(link => link.Other == other);

    /// <summary>Whether anything links this list to that account: a friendship, or a request either way.</summary>
    public bool Links(AccountId other) => Has(other) || Asked(other) || AskedBy(other);

    /// <summary>The list with every link to that account gone: what declining, withdrawing and removing all come to.</summary>
    public FriendList Without(AccountId other) => new(
        [.. Friends.Where(link => link.Other != other)],
        [.. Incoming.Where(link => link.Other != other)],
        [.. Outgoing.Where(link => link.Other != other)]);

    // Two lists are the same when they hold the same links in the same order, whatever collection holds them.
    public bool Equals(FriendList? other) =>
        other is not null && Friends.SequenceEqual(other.Friends) && Incoming.SequenceEqual(other.Incoming) && Outgoing.SequenceEqual(other.Outgoing);

    public override int GetHashCode() => HashCode.Combine(Friends.Count, Incoming.Count, Outgoing.Count);
}

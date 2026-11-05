using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Accounts.Friends;

/// <summary>What asking for, accepting or using a friend can refuse (ADR 0129). The codes are stable: the host and the page read them.</summary>
public static class FriendErrors
{
    public static readonly DomainError Self = new("Friend.Self", "You cannot be your own friend.");

    public static readonly DomainError NoSuchPlayer = new("Friend.NoSuchPlayer", "No player has that name. Check the spelling: it is the name on their cards.");

    public static readonly DomainError AlreadyFriends = new("Friend.AlreadyFriends", "You are already friends.");

    public static readonly DomainError AlreadyAsked = new("Friend.AlreadyAsked", "You already asked them. They have not answered yet.");

    public static readonly DomainError NoSuchRequest = new("Friend.NoSuchRequest", "There is no such request: it was answered, withdrawn, or never made.");

    public static readonly DomainError NotFriends = new("Friend.NotFriends", "Only a friend can be invited. Ask them to be your friend first.");

    public static readonly DomainError TooMany = new("Friend.TooMany", $"A player keeps at most {Friends.MostLinks} friends and {Friends.MostLinks} pending requests, and one of you is at the limit.");
}

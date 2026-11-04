using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Friends;

/// <summary>
/// What asking somebody to be a friend came to (ADR 0129): a request they have yet to answer, or, when they had
/// already asked, a friendship, since asking back is accepting. <paramref name="Other" /> is the account asked.
/// </summary>
public sealed record FriendAsked(AccountId Other, bool Accepted);

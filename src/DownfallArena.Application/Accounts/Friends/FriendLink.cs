using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Friends;

/// <summary>The other account of a friendship or a request, and when it was made (ADR 0129). An id, never a name: a rename or a deletion changes nothing here.</summary>
public sealed record FriendLink(AccountId Other, DateTimeOffset Since);

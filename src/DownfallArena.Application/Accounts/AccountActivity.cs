using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// When an account last signed in and when it last used the site (ADR 0174): what the operator reads to see who
/// still plays. Kept beside the account and forgotten with it. Either is null until it first happens.
/// </summary>
public sealed record AccountActivity(AccountId Account, DateTimeOffset? LastSignIn, DateTimeOffset? LastSeen);

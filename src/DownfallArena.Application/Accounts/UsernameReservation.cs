using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>A name held for an account: what makes a name unique, written before the account it names.</summary>
public sealed record UsernameReservation(Username Username, AccountId Account, DateTimeOffset ReservedAt);

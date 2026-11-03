using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// Who held one seat when the match ended: an account, or a bot by its spec. Both are null for a person who
/// played without an account (a host without accounts, or a table older than them).
/// </summary>
public sealed record ResultSeat(string Seat, AccountId? Account, string? Agent);

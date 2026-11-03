using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>What a valid session cookie says: whose it is, when it was issued, under which generation.</summary>
internal sealed record SessionClaim(AccountId Account, DateTimeOffset IssuedAt, int Generation);

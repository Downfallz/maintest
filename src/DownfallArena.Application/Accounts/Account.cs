using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// A player's account (ADR 0122): an internal id, the sign-in it belongs to, and the name it chose. Nothing else
/// is kept -- no email, no real name, no picture. <see cref="Generation" /> is what a session is checked against,
/// so raising it signs the account out everywhere.
/// </summary>
public sealed record Account(AccountId Id, ExternalIdentity Identity, Username Username, DateTimeOffset CreatedAt, int Generation);

using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// A player's account (ADR 0122): an internal id, the identities that sign in to it (one or more, ADR 0204),
/// and the name it chose. Nothing else is kept -- no email, no real name, no picture. <see cref="Generation" /> is what a session is checked against,
/// so raising it signs the account out everywhere. A suspended account (ADR 0178) cannot sign in until it is restored.
/// </summary>
public sealed record Account(AccountId Id, AccountIdentities Identities, Username Username, DateTimeOffset CreatedAt, int Generation)
{
    /// <summary>Why the operator suspended the account, and since when; none for an account in good standing.</summary>
    public AccountSuspension? Suspension { get; init; }
}

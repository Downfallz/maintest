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

    /// <summary>
    /// An identity removed from the account whose binding may still name it (#1216): written with the removal, before
    /// the binding goes, and cleared once it has. Whatever stops between the two, the next link, removal or deletion of
    /// the account removes the binding, so the provider's identifier is not kept with no owner. None most of the time.
    /// </summary>
    public ExternalIdentity? Unbinding { get; init; }

    /// <summary>
    /// Whether the account is a guest (ADR 0207): one the host made without any provider, which plays the tutorials and
    /// nothing else until a provider's sign-in upgrades it.
    /// </summary>
    public bool IsGuest => Identities.ByProvider(ExternalIdentity.Guest) is not null;
}

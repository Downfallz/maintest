using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Accounts;

/// <summary>What registering an account can refuse (ADR 0122). The codes are stable: the host and the page read them.</summary>
public static class AccountErrors
{
    public static readonly DomainError UsernameInvalid = new(
        "Account.UsernameInvalid",
        $"A player name is {Username.MinimumLength} to {Username.MaximumLength} characters: letters, digits, _ and -, starting with a letter.");

    public static readonly DomainError UsernameReserved = new("Account.UsernameReserved", "That name is reserved. Choose another.");

    public static readonly DomainError UsernameTaken = new("Account.UsernameTaken", "Another player already has that name. Choose another.");

    public static readonly DomainError AlreadyRegistered = new("Account.AlreadyRegistered", "This sign-in already has an account.");

    /// <summary>An identity linked to an account while another account holds it (ADR 0204).</summary>
    public static readonly DomainError IdentityTaken = new("Account.IdentityTaken", "Another account already uses this way to sign in. Sign in to that account and remove it there first.");

    /// <summary>
    /// A second identity from a provider the account already signs in with (sign-in methods plan, P4): one way in per
    /// provider, so a player names a way by its provider. An account lists at most one way per provider the host offers.
    /// </summary>
    public static readonly DomainError ProviderLinked = new("Account.ProviderLinked", "This account already has a way to sign in with that service. Remove it first to add another.");

    /// <summary>Removing the only way into an account (ADR 0204): deleting the account is how it goes.</summary>
    public static readonly DomainError LastIdentity = new("Account.LastIdentity", "This is the only way to sign in to this account. Add another way before you remove it.");

    /// <summary>Removing an identity the account does not hold.</summary>
    public static readonly DomainError IdentityNotLinked = new("Account.IdentityNotLinked", "This account does not use that way to sign in.");

    public static readonly DomainError NotFound = new("Account.NotFound", "There is no such account.");

    /// <summary>A sign-in refused because the operator suspended the account (ADR 0178), saying why.</summary>
    public static DomainError Suspended(AccountSuspension suspension)
    {
        ArgumentNullException.ThrowIfNull(suspension);
        return new DomainError("Account.Suspended", $"This account is suspended: {suspension.Reason}");
    }

    /// <summary>Restoring an account that is not suspended.</summary>
    public static readonly DomainError NotSuspended = new("Account.NotSuspended", "This account is not suspended.");

    /// <summary>Suspending an account that already is, which keeps the suspension it has.</summary>
    public static readonly DomainError AlreadySuspended = new("Account.AlreadySuspended", "This account is already suspended. Restore it first to suspend it for another reason.");

    /// <summary>A new account refused while the operator has registration closed (ADR 0177).</summary>
    public static readonly DomainError RegistrationClosed = new("Account.RegistrationClosed", "New accounts are closed for now. Try again later.");

    public static readonly DomainError PreferencesInvalid = new(
        "Account.PreferencesInvalid",
        "Game settings take a pace of auto or step and a speed of 0.5, 1, 1.5 or 2.");
}

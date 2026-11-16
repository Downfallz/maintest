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

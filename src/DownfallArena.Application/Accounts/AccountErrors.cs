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

    /// <summary>A new account refused while the operator has registration closed (ADR 0177).</summary>
    public static readonly DomainError RegistrationClosed = new("Account.RegistrationClosed", "New accounts are closed for now. Try again later.");

    public static readonly DomainError PreferencesInvalid = new(
        "Account.PreferencesInvalid",
        "Game settings take a pace of auto or step and a speed of 0.5, 1, 1.5 or 2.");
}

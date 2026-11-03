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
}

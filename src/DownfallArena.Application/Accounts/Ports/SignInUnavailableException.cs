namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Thrown by a way to sign in that could not ask its provider (sign-in methods plan, P3): the provider out of reach,
/// answering an error of its own or asking the host to slow down, or the host holding back its own calls. Not a
/// refusal: the proof may be good, and the player is told to try again shortly rather than to sign in again. The
/// route answers 503 and says so on its own console line, so an outage is never counted as players refused.
/// </summary>
public sealed class SignInUnavailableException : Exception
{
    public SignInUnavailableException()
    {
    }

    public SignInUnavailableException(string message)
        : base(message)
    {
    }

    public SignInUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Whether the provider was asked and answered, though not with a verdict on the player: the app itself refused.
    /// The sign-in spent the provider's budget, so the route counts it against the address as a refusal, while still
    /// answering 503: otherwise one address could spend the budget of every player without its count rising.
    /// </summary>
    public bool ProviderAnswered { get; init; }

    /// <summary>
    /// How soon the same proof may be posted again, when the provider was never asked and only the host's own limit
    /// stood in the way for a moment: the route says it as <c>Retry-After</c>, and the return page waits it out once
    /// (#1204). Null when nothing says the wait is short.
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }
}

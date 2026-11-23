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
}

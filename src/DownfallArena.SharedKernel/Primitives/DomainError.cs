namespace DownfallArena.Domain.Common;

/// <summary>
/// A domain error with a stable, machine-readable code and a human-readable message.
/// Codes are namespaced by aggregate, e.g. <c>Match.AlreadyStarted</c>.
/// </summary>
public sealed record DomainError(string Code, string Message)
{
    public static readonly DomainError None = new(string.Empty, string.Empty);
}

namespace DownfallArena.Cli.Table;

/// <summary>
/// What a request carries about its account: the session cookie, whether a cookie set back must be <c>Secure</c>, and
/// the network address it came from (<see cref="RequestAddress" />), which counts its sign-ins and is never kept.
/// </summary>
internal sealed record AccountRequest(string? Session, bool Secure, string? Address = null);

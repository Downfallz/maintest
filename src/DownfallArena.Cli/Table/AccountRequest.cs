namespace DownfallArena.Cli.Table;

/// <summary>What a request carries about its account: the session cookie, and whether a cookie set back must be <c>Secure</c>.</summary>
internal sealed record AccountRequest(string? Session, bool Secure);

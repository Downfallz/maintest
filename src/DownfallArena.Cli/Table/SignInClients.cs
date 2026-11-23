namespace DownfallArena.Cli.Table;

/// <summary>The public client id of each provider the host may sign in with; null for one it was not given.</summary>
internal sealed record SignInClients(string? Google = null, string? Discord = null);

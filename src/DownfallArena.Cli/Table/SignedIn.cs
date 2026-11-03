using DownfallArena.Application.Accounts;

namespace DownfallArena.Cli.Table;

/// <summary>Who a request is from, when anyone, and the session to set back when the one it carried was due to be renewed.</summary>
internal sealed record SignedIn(Account Account, string? Renewed);

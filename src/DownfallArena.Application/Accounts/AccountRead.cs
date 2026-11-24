namespace DownfallArena.Application.Accounts;

/// <summary>
/// An account as the store read it, with the version of what it read: a write conditional on that version is refused
/// once another writer (the release beside this one during a deploy, the hourly job) wrote or deleted the account
/// meanwhile (#1217).
/// </summary>
public sealed record AccountRead(Account Account, string Version);

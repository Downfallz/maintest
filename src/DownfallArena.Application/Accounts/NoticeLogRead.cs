namespace DownfallArena.Application.Accounts;

/// <summary>
/// An account's notice log as it was read, and the version it was read at: what a write is made over, so a write
/// another process made in between is never lost. The version is none for a log never written.
/// </summary>
public sealed record NoticeLogRead(IReadOnlyList<NoticeEntry> Entries, string? Version);

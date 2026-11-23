namespace DownfallArena.Application.Accounts;

/// <summary>
/// An account's notice log as it was read, and the version it was read at: what a write is made over, so a write
/// another process made in between is never lost. The version is none for a log never written.
/// </summary>
public sealed record NoticeLogRead(IReadOnlyList<NoticeEntry> Entries, string? Version)
{
    /// <summary>
    /// When the log read was last written, as the store reads it with the log; none where the store does not say. No
    /// entry is newer than it, those this host could not read included, so a log last written past its time holds
    /// nothing to keep (#1118).
    /// </summary>
    public DateTimeOffset? Written { get; init; }
}

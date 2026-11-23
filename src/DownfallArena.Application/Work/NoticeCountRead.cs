namespace DownfallArena.Application.Work;

/// <summary>A day's counts as they were read, and the version a write is made over; none for a day nothing was counted on.</summary>
public sealed record NoticeCountRead(IReadOnlyList<NoticeCount> Counts, string? Version);

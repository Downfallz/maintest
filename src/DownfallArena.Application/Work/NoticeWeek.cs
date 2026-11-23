namespace DownfallArena.Application.Work;

/// <summary>The notices' counts the operator reads (N6): today's, in UTC, and the seven days that end today, a kind each.</summary>
public sealed record NoticeWeek(DateOnly Today, IReadOnlyList<NoticeCount> Day, IReadOnlyList<NoticeCount> Week);

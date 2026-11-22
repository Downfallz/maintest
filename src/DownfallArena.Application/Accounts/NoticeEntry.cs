namespace DownfallArena.Application.Accounts;

/// <summary>
/// One entry of an account's notice log (ADR 0201): the notice, when it was kept, and whether the player has read it.
/// The id grows with each entry of the log, so "read up to this one" means the same on every page.
/// </summary>
public sealed record NoticeEntry(long Id, NoticeKind Kind, string Title, string Body, string Url, string Tag, DateTimeOffset At, bool Read);

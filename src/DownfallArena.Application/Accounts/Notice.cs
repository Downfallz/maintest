namespace DownfallArena.Application.Accounts;

/// <summary>
/// A notice to an account (ADR 0201): its kind, a title, a line, where a tap goes, and a tag that names the thing it
/// is about, so a later notice of the same thing replaces it, in the bell as on a phone.
/// </summary>
public sealed record Notice(NoticeKind Kind, string Title, string Body, string Url, string Tag);

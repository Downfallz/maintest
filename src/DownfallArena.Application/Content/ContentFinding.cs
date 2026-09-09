namespace DownfallArena.Application.Content;

/// <summary>
/// One thing the audit found in the content: a stable code namespaced by what it is about, the id or code it is
/// about, and what it means for the author. A finding is never a build error — the content is valid — it is
/// content that cannot be reached, cast, or used, which the builder has no reason to refuse.
/// </summary>
public sealed record ContentFinding(string Code, string Subject, string Message);

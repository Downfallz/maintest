namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The index as read, and the version a write over it names, so a write made since is not lost (#866).
/// <see cref="Content" /> is none for an index never written or one that cannot be read; <see cref="Version" /> is none
/// only for one never written, so an unreadable one is written over in place.
/// </summary>
public sealed record FeedbackIndexRead(FeedbackIndexContent? Content, string? Version);

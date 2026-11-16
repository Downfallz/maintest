using DownfallArena.Application.Accounts;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>The mute that holds on a sender after the operator asked, none once lifted, and whether the asking changed it.</summary>
public sealed record FeedbackMuteChange(FeedbackMute? Mute, bool Changed);

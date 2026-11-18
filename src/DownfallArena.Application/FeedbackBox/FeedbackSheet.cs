using DownfallArena.Application.Accounts;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>What the operator's sheet shows beside the record: its group, the mute that holds on its sender, and whether one can.</summary>
public sealed record FeedbackSheet(FeedbackGroup Group, FeedbackMute? Mute, bool CanMute);

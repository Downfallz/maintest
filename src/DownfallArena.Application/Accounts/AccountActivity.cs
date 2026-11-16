using DownfallArena.Application.FeedbackBox;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// When an account last signed in and when it last used the site (ADR 0174): what the operator reads to see who
/// still plays. Kept beside the account and forgotten with it. Either is null until it first happens.
/// <see cref="FeedbackMute" /> is the account's mute from feedback, kept here so it goes with the account
/// (docs/tabletop/feedback-plan.md, "Against spam"); none while the account may send feedback.
/// </summary>
public sealed record AccountActivity(AccountId Account, DateTimeOffset? LastSignIn, DateTimeOffset? LastSeen)
{
    public FeedbackMute? FeedbackMute { get; init; }
}

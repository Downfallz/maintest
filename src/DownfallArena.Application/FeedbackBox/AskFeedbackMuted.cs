namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// Whether a sender is muted now (docs/tabletop/feedback-plan.md, "Against spam"): what a page asks before its player
/// writes, so a muted one is offered nothing (#717). <see cref="SendFeedback" /> asks the same before it keeps anything.
/// </summary>
public sealed class AskFeedbackMuted(FeedbackMutes mutes, TimeProvider clock)
{
    public async Task<bool> HandleAsync(string sender, string? account, CancellationToken cancellationToken = default) =>
        await mutes.FindAsync(sender, account, clock.GetUtcNow(), cancellationToken) is not null;
}

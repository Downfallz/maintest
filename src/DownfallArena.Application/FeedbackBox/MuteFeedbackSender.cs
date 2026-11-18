using DownfallArena.Application.Accounts;
using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>The sender of a piece of feedback muted by the operator (docs/tabletop/feedback-plan.md, R3).</summary>
public sealed class MuteFeedbackSender(IFeedbackStore store, FeedbackMutes mutes, TimeProvider clock)
{
    /// <summary>Stops the feedback's sender from sending more for <see cref="FeedbackMute.Lasts" />; a mute that holds already is kept as it is.</summary>
    public async Task<Result<FeedbackMuteChange>> HandleAsync(string id, string by, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        var sender = await mutes.MuteableAsync(store, id, cancellationToken);
        if (sender.IsFailure)
        {
            return Result.Failure<FeedbackMuteChange>(sender.Error);
        }

        var feedback = sender.Value;
        var now = clock.GetUtcNow();
        if (await mutes.OfSenderAsync(feedback, now, cancellationToken) is { } holding)
        {
            return Result.Success(new FeedbackMuteChange(holding, Changed: false));
        }

        var mute = FeedbackMute.Begun(now, by, FeedbackMute.ByOperator);
        await mutes.SetAsync(feedback.Sender, feedback.Account, mute, cancellationToken);
        return Result.Success(new FeedbackMuteChange(mute, Changed: true));
    }
}

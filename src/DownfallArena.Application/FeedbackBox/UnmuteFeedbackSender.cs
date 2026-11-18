using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>The sender of a piece of feedback let speak again (docs/tabletop/feedback-plan.md, R3).</summary>
public sealed class UnmuteFeedbackSender(IFeedbackStore store, FeedbackMutes mutes, TimeProvider clock)
{
    /// <summary>Lets the feedback's sender send again; nothing changes for one no mute holds on.</summary>
    public async Task<Result<FeedbackMuteChange>> HandleAsync(string id, CancellationToken cancellationToken = default)
    {
        var sender = await FeedbackRecords.MuteableAsync(store, mutes, id, cancellationToken);
        if (sender.IsFailure)
        {
            return Result.Failure<FeedbackMuteChange>(sender.Error);
        }

        var feedback = sender.Value;
        if (await mutes.OfSenderAsync(feedback, clock.GetUtcNow(), cancellationToken) is null)
        {
            return Result.Success(new FeedbackMuteChange(null, Changed: false));
        }

        await mutes.SetAsync(feedback.Sender, feedback.Account, null, cancellationToken);
        return Result.Success(new FeedbackMuteChange(null, Changed: true));
    }
}

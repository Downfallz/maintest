using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>The scenario <b>Reproduce</b> cut from a bug report, kept with it (docs/tabletop/feedback-plan.md, R4).</summary>
public sealed class KeepReproduction(IFeedbackStore store, FeedbackWrites writes)
{
    /// <summary>
    /// Keeps on a bug report the scenario <b>Reproduce</b> cut from it, so the sheet links it and the operator's next
    /// press plays it again rather than cutting another. Only a bug report is reproduced; its status is the operator's
    /// to set once they have played the round. When another press kept a scenario since, one
    /// <paramref name="stillKept" /> finds, that one stays and the report is answered as it is: the caller lets its own
    /// cut go, so two presses at once leave one scenario.
    /// </summary>
    public Task<Result<Feedback>> HandleAsync(string id, string scenario, Func<string, Task<bool>> stillKept, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        ArgumentNullException.ThrowIfNull(stillKept);
        return writes.OneAtATimeAsync(() => KeepingAsync(id, scenario, stillKept, cancellationToken), cancellationToken);
    }

    private async Task<Result<Feedback>> KeepingAsync(string id, string scenario, Func<string, Task<bool>> stillKept, CancellationToken cancellationToken)
    {
        if (await FeedbackRecords.FindAsync(store, id, cancellationToken) is not { } feedback)
        {
            return Result.Failure<Feedback>(FeedbackErrors.NotFound(id));
        }

        if (feedback.Kind != FeedbackKind.Bug)
        {
            return Result.Failure<Feedback>(FeedbackErrors.NotABug);
        }

        if (feedback.Reproduction is { } kept && (kept == scenario || await stillKept(kept)))
        {
            return Result.Success(feedback);
        }

        var reproduced = feedback with { Reproduction = scenario };
        await store.SaveAsync(reproduced, cancellationToken);
        return Result.Success(reproduced);
    }
}

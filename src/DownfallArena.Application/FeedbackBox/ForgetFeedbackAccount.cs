using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// An account deleted (ADR 0189): its feedback keeps its text and loses the account, and the sender it was counted
/// against becomes one that names nobody.
/// </summary>
public sealed class ForgetFeedbackAccount(IFeedbackStore store)
{
    /// <summary>What a deleted account's feedback is counted against from then on.</summary>
    public const string Gone = "deleted";

    /// <summary>Drops the account from each piece of feedback it sent, and answers how many it had.</summary>
    public async Task<int> HandleAsync(string account, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        var forgotten = 0;
        foreach (var id in await store.IdsAsync(cancellationToken))
        {
            if (await store.FindAsync(id, cancellationToken) is { } feedback && (feedback.Account == account || feedback.Sender == account))
            {
                await store.SaveAsync(feedback with { Account = null, Sender = Gone }, cancellationToken);
                forgotten++;
            }
        }

        return forgotten;
    }
}

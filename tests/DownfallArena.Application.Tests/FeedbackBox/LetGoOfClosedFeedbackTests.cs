using DownfallArena.Application.FeedbackBox;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>Closed feedback is kept 180 days, then the tending pass lets it go (ADR 0189); open feedback stays.</summary>
public sealed class LetGoOfClosedFeedbackTests
{
    private static readonly DateTimeOffset Sent = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _clock = new(Sent.AddDays(400));
    private readonly KeptFeedback _store = new();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static Feedback Kept(FeedbackStatus status, DateTimeOffset? closed) => new()
    {
        Id = Feedback.NewId(Sent),
        Kind = FeedbackKind.Bug,
        Status = status,
        At = Sent,
        ClosedAt = closed,
        Sender = "acct_1",
        Category = "rules",
        Text = "The bleed did not tick after the stun.",
    };

    [Fact]
    public async Task Feedback_closed_more_than_180_days_ago_is_deleted_and_the_rest_kept()
    {
        var old = Kept(FeedbackStatus.Fixed, _clock.GetUtcNow().AddDays(-181));
        var recent = Kept(FeedbackStatus.Dismissed, _clock.GetUtcNow().AddDays(-179));
        var open = Kept(FeedbackStatus.Seen, closed: null);
        foreach (var feedback in new[] { old, recent, open })
        {
            await _store.SaveAsync(feedback, Cancel);
        }

        var gone = await new LetGoOfClosedFeedback(_store, _clock).HandleAsync(Cancel);

        gone.ShouldBe(1);
        _store.All.Select(one => one.Id).ShouldBe([recent.Id, open.Id], ignoreOrder: true);
    }
}

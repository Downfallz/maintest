using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;
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

    [Fact]
    public async Task A_merged_idea_goes_with_the_idea_it_joined_and_stays_while_that_one_is_open()
    {
        var shipped = Kept(FeedbackStatus.Shipped, _clock.GetUtcNow().AddDays(-181)) with { Kind = FeedbackKind.Idea };
        var gone = Kept(FeedbackStatus.Seen, closed: null) with { Kind = FeedbackKind.Idea, MergedInto = shipped.Id };
        var open = Kept(FeedbackStatus.Planned, closed: null) with { Kind = FeedbackKind.Idea };
        var staying = Kept(FeedbackStatus.Seen, closed: null) with { Kind = FeedbackKind.Idea, MergedInto = open.Id };
        foreach (var feedback in new[] { shipped, gone, open, staying })
        {
            await _store.SaveAsync(feedback, Cancel);
        }

        var deleted = await new LetGoOfClosedFeedback(_store, _clock).HandleAsync(Cancel);

        deleted.ShouldBe(2);
        _store.All.Select(one => one.Id).ShouldBe([open.Id, staying.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task A_merged_idea_whose_group_s_idea_cannot_be_read_is_kept()
    {
        var unreadable = Kept(FeedbackStatus.Seen, closed: null) with { Kind = FeedbackKind.Idea };
        var joined = Kept(FeedbackStatus.Seen, closed: null) with { Kind = FeedbackKind.Idea, MergedInto = unreadable.Id };
        await _store.SaveAsync(unreadable, Cancel);
        await _store.SaveAsync(joined, Cancel);

        var deleted = await new LetGoOfClosedFeedback(new Unreadable(_store, unreadable.Id), _clock).HandleAsync(Cancel);

        deleted.ShouldBe(0, "a record listed but unreadable is not gone: what joined it stays");
    }

    /// <summary>A store that lists a record it cannot read, as a store does with a document that no longer parses.</summary>
    private sealed class Unreadable(IFeedbackStore inner, string id) : IFeedbackStore
    {
        public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default) => inner.IdsAsync(cancellationToken);

        public Task<IReadOnlyList<string>> IdsSentAsync(DateTimeOffset after, DateTimeOffset until, CancellationToken cancellationToken = default) => inner.IdsSentAsync(after, until, cancellationToken);

        public Task<Feedback?> FindAsync(string wanted, CancellationToken cancellationToken = default) =>
            wanted == id ? Task.FromResult<Feedback?>(null) : inner.FindAsync(wanted, cancellationToken);

        public Task SaveAsync(Feedback feedback, CancellationToken cancellationToken = default) => inner.SaveAsync(feedback, cancellationToken);

        public Task DeleteAsync(string wanted, CancellationToken cancellationToken = default) => inner.DeleteAsync(wanted, cancellationToken);
    }
}

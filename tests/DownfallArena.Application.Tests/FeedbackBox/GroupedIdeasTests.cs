using DownfallArena.Application.FeedbackBox;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>
/// Ideas grouped (docs/tabletop/feedback-plan.md, I1): an idea merged into another keeps its sender and words and adds
/// to the count, the list sorts by how many asked, and an idea is planned, shipped or declined.
/// </summary>
public sealed class GroupedIdeasTests : IDisposable
{
    private const string Plan = "docs/tabletop/replay-plan.md";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly KeptFeedback _store = new();
    private readonly FeedbackWrites _writes = new();

    public void Dispose() => _writes.Dispose();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private OperatorFeedback Desk() => new(_store, new FeedbackMutes(null), _writes, _clock);

    private async Task<Feedback> IdeaAsync(string sender, string text = "I wanted to see the last round again but could not.", FeedbackKind kind = FeedbackKind.Idea)
    {
        var idea = new Feedback
        {
            Id = Feedback.NewId(_clock.GetUtcNow()),
            Kind = kind,
            At = _clock.GetUtcNow(),
            Sender = sender,
            Category = kind == FeedbackKind.Idea ? "battle" : "rules",
            Text = text,
        };
        await _store.SaveAsync(idea, Cancel);
        _clock.Advance(TimeSpan.FromMinutes(1));
        return idea;
    }

    private async Task<FeedbackPage> IdeasAsync() => await Desk().PageAsync(new FeedbackFilter(FeedbackKind.Idea, OpenOnly: true), 1, Cancel);

    [Fact]
    public async Task A_merge_keeps_every_sender_and_words_and_adds_to_the_count()
    {
        var first = await IdeaAsync("seat-a");
        var second = await IdeaAsync("seat-b", "Let me watch a round again.");
        var third = await IdeaAsync("seat-c", "A replay of the round, please.");

        await Desk().MergeAsync(second.Id, first.Id, Cancel);
        await Desk().MergeAsync(third.Id, first.Id, Cancel);

        var group = await Desk().GroupAsync(first, Cancel);
        group.Merged.Select(one => (one.Id, one.Sender, one.Text)).ShouldBe([(second.Id, "seat-b", second.Text), (third.Id, "seat-c", third.Text)]);
        group.Asks.ShouldBe(new FeedbackAsks(AskedBy: 3, Since: first.At, Latest: third.At));
    }

    [Fact]
    public async Task A_merged_idea_leaves_the_list_and_is_read_inside_the_one_it_joined()
    {
        var first = await IdeaAsync("seat-a");
        var second = await IdeaAsync("seat-b");

        var merged = (await Desk().MergeAsync(second.Id, first.Id, Cancel)).Value;

        (merged.Changed, merged.Feedback.MergedInto, merged.Feedback.Status, merged.Into.Id).ShouldBe((true, first.Id, FeedbackStatus.Seen, first.Id));
        var page = await IdeasAsync();
        page.Rows.ShouldHaveSingleItem().Id.ShouldBe(first.Id);
        page.Asks[first.Id].AskedBy.ShouldBe(2);
        (await Desk().GroupAsync(merged.Feedback, Cancel)).Into.ShouldNotBeNull().Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task A_merge_into_a_bug_report_into_itself_or_into_an_idea_merged_into_it_is_refused()
    {
        var idea = await IdeaAsync("seat-a");
        var other = await IdeaAsync("seat-b");
        var report = await IdeaAsync("seat-c", "The bleed did not tick.", FeedbackKind.Bug);
        await Desk().MergeAsync(other.Id, idea.Id, Cancel);

        (await Desk().MergeAsync(idea.Id, report.Id, Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        (await Desk().MergeAsync(report.Id, idea.Id, Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        (await Desk().MergeAsync(idea.Id, idea.Id, Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        (await Desk().MergeAsync(idea.Id, other.Id, Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        (await Desk().MergeAsync(idea.Id, Feedback.NewId(_clock.GetUtcNow()), Cancel)).Error.Code.ShouldBe("Feedback.NotFound");
    }

    [Fact]
    public async Task An_idea_merged_into_one_that_was_merged_since_joins_the_whole_group()
    {
        var first = await IdeaAsync("seat-a");
        var second = await IdeaAsync("seat-b");
        var third = await IdeaAsync("seat-c");

        await Desk().MergeAsync(first.Id, second.Id, Cancel);
        await Desk().MergeAsync(second.Id, third.Id, Cancel);
        var late = await IdeaAsync("seat-d");
        var joined = (await Desk().MergeAsync(late.Id, first.Id, Cancel)).Value;

        joined.Into.Id.ShouldBe(third.Id, "a merge into a merged idea joins the one its group became");
        (await IdeasAsync()).Asks[third.Id].AskedBy.ShouldBe(4);
    }

    [Fact]
    public async Task One_player_asking_twice_is_asked_by_one()
    {
        var first = await IdeaAsync("seat-a");
        var again = await IdeaAsync("seat-a");

        await Desk().MergeAsync(again.Id, first.Id, Cancel);

        (await IdeasAsync()).Asks[first.Id].AskedBy.ShouldBe(1);
    }

    [Fact]
    public async Task Merging_again_changes_nothing()
    {
        var first = await IdeaAsync("seat-a");
        var second = await IdeaAsync("seat-b");
        await Desk().MergeAsync(second.Id, first.Id, Cancel);

        (await Desk().MergeAsync(second.Id, first.Id, Cancel)).Value.Changed.ShouldBeFalse();
    }

    [Fact]
    public async Task Ideas_are_listed_by_how_many_asked_then_by_the_newest_ask()
    {
        var once = await IdeaAsync("seat-a");
        var twice = await IdeaAsync("seat-b");
        var newest = await IdeaAsync("seat-c");
        var joining = await IdeaAsync("seat-d");
        await Desk().MergeAsync(joining.Id, twice.Id, Cancel);

        (await IdeasAsync()).Rows.Select(one => one.Id).ShouldBe([twice.Id, newest.Id, once.Id]);
    }

    [Fact]
    public async Task A_merged_idea_takes_no_status_and_one_others_joined_stays_an_idea()
    {
        var first = await IdeaAsync("seat-a");
        var second = await IdeaAsync("seat-b");
        await Desk().MergeAsync(second.Id, first.Id, Cancel);

        (await Desk().SetStatusAsync(second.Id, FeedbackStatus.Seen, null, "mark", Cancel)).Error.Code.ShouldBe("Feedback.MergedAway");
        (await Desk().ChangeKindAsync(second.Id, FeedbackKind.Bug, Cancel)).Error.Code.ShouldBe("Feedback.MergedAway");
        (await Desk().ChangeKindAsync(first.Id, FeedbackKind.Bug, Cancel)).Error.Code.ShouldBe("Feedback.Grouped");
    }

    [Fact]
    public async Task A_merge_into_a_closed_idea_is_refused()
    {
        var declined = await IdeaAsync("seat-a");
        var late = await IdeaAsync("seat-b");
        await Desk().SetStatusAsync(declined.Id, FeedbackStatus.Declined, "Not for this game.", "mark", Cancel);

        (await Desk().MergeAsync(late.Id, declined.Id, Cancel)).Error.Code.ShouldBe("Feedback.Closed");
    }

    [Fact]
    public async Task An_idea_is_planned_with_its_plan_s_path_and_shipped_keeps_it()
    {
        var idea = await IdeaAsync("seat-a");

        (await Desk().PlanAsync(idea.Id, "../secrets.md", Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        (await Desk().PlanAsync(idea.Id, "docs/tabletop/replay.md", Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        (await Desk().SetStatusAsync(idea.Id, FeedbackStatus.Planned, null, "mark", Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        var planned = (await Desk().PlanAsync(idea.Id, Plan, Cancel)).Value.Feedback;
        (planned.Status, planned.Plan, planned.IsClosed).ShouldBe((FeedbackStatus.Planned, Plan, false));

        var shipped = (await Desk().SetStatusAsync(idea.Id, FeedbackStatus.Shipped, null, "mark", Cancel)).Value.Feedback;

        (shipped.Status, shipped.Plan, shipped.ClosedAt).ShouldBe((FeedbackStatus.Shipped, Plan, _clock.GetUtcNow()));
    }

    [Fact]
    public async Task An_idea_is_declined_with_a_reason_of_one_line_and_that_closes_it()
    {
        var idea = await IdeaAsync("seat-a");

        (await Desk().SetStatusAsync(idea.Id, FeedbackStatus.Declined, " ", "mark", Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        var declined = (await Desk().SetStatusAsync(idea.Id, FeedbackStatus.Declined, "A replay is in the session's page already.", "mark", Cancel)).Value;

        (declined.Changed, declined.Feedback.Status, declined.Feedback.Reason, declined.Feedback.IsClosed, declined.Feedback.IsSpam).ShouldBe((true, FeedbackStatus.Declined, "A replay is in the session's page already.", true, false));
        (await Desk().SetStatusAsync(idea.Id, FeedbackStatus.Declined, "A replay is in the session's page already.", "mark", Cancel)).Value.Changed.ShouldBeFalse();
    }

    [Fact]
    public async Task A_bug_report_is_neither_planned_shipped_nor_declined()
    {
        var report = await IdeaAsync("seat-a", "The bleed did not tick.", FeedbackKind.Bug);

        (await Desk().PlanAsync(report.Id, Plan, Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        (await Desk().SetStatusAsync(report.Id, FeedbackStatus.Shipped, null, "mark", Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        (await Desk().SetStatusAsync(report.Id, FeedbackStatus.Declined, "No.", "mark", Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
    }

    [Fact]
    public async Task A_merged_idea_waits_on_nobody()
    {
        var first = await IdeaAsync("seat-a");
        var second = await IdeaAsync("seat-b");

        await Desk().MergeAsync(second.Id, first.Id, Cancel);

        (await Desk().WaitingAsync(Cancel)).New.ShouldBe(1);
    }
}

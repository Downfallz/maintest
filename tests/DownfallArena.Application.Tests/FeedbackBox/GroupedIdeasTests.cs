using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;
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

    private OperatorDesk Desk() => new(_store, new FeedbackMutes(null), _writes, _clock);

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
        (await _store.FindAsync(first.Id, Cancel))!.MergedInto.ShouldBe(third.Id, "every merged idea names its group's idea itself");
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

    [Fact]
    public async Task A_plan_is_kept_until_another_plan_replaces_it()
    {
        var idea = await IdeaAsync("seat-a");
        await Desk().PlanAsync(idea.Id, Plan, Cancel);

        await Desk().SetStatusAsync(idea.Id, FeedbackStatus.Declined, "A misclick.", "mark", Cancel);
        await Desk().SetStatusAsync(idea.Id, FeedbackStatus.Seen, null, "mark", Cancel);
        var shipped = (await Desk().SetStatusAsync(idea.Id, FeedbackStatus.Shipped, null, "mark", Cancel)).Value.Feedback;

        shipped.Plan.ShouldBe(Plan);
        (await Desk().PlanAsync(idea.Id, "docs/tabletop/replay-again-plan.md", Cancel)).Value.Feedback.Plan.ShouldBe("docs/tabletop/replay-again-plan.md");
    }

    [Fact]
    public async Task An_idea_merged_into_its_group_by_another_road_changes_nothing()
    {
        var first = await IdeaAsync("seat-a");
        var second = await IdeaAsync("seat-b");
        var third = await IdeaAsync("seat-c");
        await Desk().MergeAsync(third.Id, second.Id, Cancel);
        await Desk().MergeAsync(second.Id, first.Id, Cancel);

        var again = (await Desk().MergeAsync(third.Id, first.Id, Cancel)).Value;

        (again.Changed, again.Into.Id).ShouldBe((false, first.Id));
    }

    [Fact]
    public async Task A_merged_idea_dismissed_as_spam_leaves_its_group_and_counts_against_its_sender()
    {
        var first = await IdeaAsync("seat-a");
        var flood = await IdeaAsync("seat-b");
        await Desk().MergeAsync(flood.Id, first.Id, Cancel);

        var dismissed = (await Desk().SetStatusAsync(flood.Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel)).Value.Feedback;

        (dismissed.MergedInto, dismissed.IsSpam, dismissed.SpamCounted).ShouldBe((null, true, true));
        (await IdeasAsync()).Asks[first.Id].AskedBy.ShouldBe(1);
    }

    [Fact]
    public async Task A_player_s_next_idea_is_not_folded_into_one_merged_away()
    {
        var first = await IdeaAsync("seat-a");
        var mine = await IdeaAsync("seat-b");
        await Desk().MergeAsync(mine.Id, first.Id, Cancel);
        _clock.Advance(SendFeedback.Apart);

        var answer = await new SendFeedback(_store, new GuestAddresses(), _writes, _clock).HandleAsync(
            new FeedbackSent { Kind = FeedbackKind.Idea, Sender = "seat-b", Category = "lobby", Text = "I wanted to invite a friend by name." }, Cancel);

        answer.Merged.ShouldBeFalse();
    }

    [Fact]
    public async Task Whether_others_joined_an_idea_is_read_off_its_own_record_not_the_whole_store()
    {
        var idea = await IdeaAsync("seat-a");
        var other = await IdeaAsync("seat-b");
        var counting = new CountsListings(_store);
        var desk = new OperatorDesk(counting, new FeedbackMutes(null), _writes, _clock);
        await desk.MergeAsync(other.Id, idea.Id, Cancel);

        (await desk.ChangeKindAsync(idea.Id, FeedbackKind.Bug, Cancel)).Error.Code.ShouldBe("Feedback.Grouped");

        counting.Listings.ShouldBe(0, "players' sending waits on the write gate: nothing under it reads every record");
        (await _store.FindAsync(idea.Id, Cancel))!.Joined.ShouldBe([other.Id]);
    }

    [Fact]
    public async Task An_idea_others_joined_takes_them_along_when_it_is_merged_and_dismissing_it_leaves_them()
    {
        var early = await IdeaAsync("seat-a");
        var middle = await IdeaAsync("seat-b");
        var group = await IdeaAsync("seat-c");
        await Desk().MergeAsync(early.Id, middle.Id, Cancel);
        await Desk().MergeAsync(middle.Id, group.Id, Cancel);

        await Desk().SetStatusAsync(middle.Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel);

        (await _store.FindAsync(early.Id, Cancel))!.MergedInto.ShouldBe(group.Id);
        (await _store.FindAsync(group.Id, Cancel))!.Joined.ShouldBe([early.Id]);
        (await IdeasAsync()).Asks[group.Id].AskedBy.ShouldBe(2, "the honest player's idea stays in the group the spam left");
    }

    [Fact]
    public async Task An_idea_moved_to_another_group_leaves_the_first()
    {
        var first = await IdeaAsync("seat-a");
        var second = await IdeaAsync("seat-b");
        var moving = await IdeaAsync("seat-c");
        await Desk().MergeAsync(moving.Id, first.Id, Cancel);

        await Desk().MergeAsync(moving.Id, second.Id, Cancel);

        (await _store.FindAsync(first.Id, Cancel))!.Joined.ShouldBeEmpty();
        (await _store.FindAsync(second.Id, Cancel))!.Joined.ShouldBe([moving.Id]);
        (await Desk().ChangeKindAsync(first.Id, FeedbackKind.Bug, Cancel)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_merge_cut_short_by_the_store_and_asked_again_leaves_a_whole_group()
    {
        var group = await IdeaAsync("seat-a");
        var idea = await IdeaAsync("seat-b");
        var failing = new FailsOnSave(_store, failOn: 2);
        var desk = new OperatorDesk(failing, new FeedbackMutes(null), _writes, _clock);
        await Should.ThrowAsync<IOException>(() => desk.MergeAsync(idea.Id, group.Id, Cancel));

        var again = (await desk.MergeAsync(idea.Id, group.Id, Cancel)).Value;

        again.Changed.ShouldBeTrue("the retry finishes what the cut left, and is noted");
        (await desk.ChangeKindAsync(group.Id, FeedbackKind.Bug, Cancel)).Error.Code.ShouldBe("Feedback.Grouped");
        (await desk.GroupAsync(group, Cancel)).Merged.Select(one => one.Id).ShouldBe([idea.Id]);
    }

    [Fact]
    public async Task A_list_naming_an_idea_that_left_does_not_hold_the_group_s_idea()
    {
        var group = await IdeaAsync("seat-a");
        var left = await IdeaAsync("seat-b");
        await _store.SaveAsync((await _store.FindAsync(group.Id, Cancel))! with { Joined = [left.Id] }, Cancel);

        var changed = await Desk().ChangeKindAsync(group.Id, FeedbackKind.Bug, Cancel);

        changed.IsSuccess.ShouldBeTrue("only ideas that still name it hold it an idea");
        (await Desk().GroupAsync(left, Cancel)).Into.ShouldBeNull();
    }

    /// <summary>A store whose Nth save fails, as a storage hiccup between two records would.</summary>
    private sealed class FailsOnSave(IFeedbackStore inner, int failOn) : IFeedbackStore
    {
        private int _saves;

        public Task SaveAsync(Feedback feedback, CancellationToken cancellationToken = default) =>
            ++_saves == failOn ? throw new IOException("The storage did not answer.") : inner.SaveAsync(feedback, cancellationToken);

        public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default) => inner.IdsAsync(cancellationToken);

        public Task<IReadOnlyList<string>> IdsSentAsync(DateTimeOffset after, DateTimeOffset until, CancellationToken cancellationToken = default) => inner.IdsSentAsync(after, until, cancellationToken);

        public Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default) => inner.FindAsync(id, cancellationToken);

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => inner.DeleteAsync(id, cancellationToken);
    }

    /// <summary>A store that counts how often every record is listed.</summary>
    private sealed class CountsListings(IFeedbackStore inner) : IFeedbackStore
    {
        public int Listings { get; private set; }

        public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default)
        {
            Listings++;
            return inner.IdsAsync(cancellationToken);
        }

        public Task<IReadOnlyList<string>> IdsSentAsync(DateTimeOffset after, DateTimeOffset until, CancellationToken cancellationToken = default)
        {
            Listings++;
            return inner.IdsSentAsync(after, until, cancellationToken);
        }

        public Task SaveAsync(Feedback feedback, CancellationToken cancellationToken = default) => inner.SaveAsync(feedback, cancellationToken);

        public Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default) => inner.FindAsync(id, cancellationToken);

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => inner.DeleteAsync(id, cancellationToken);
    }
}

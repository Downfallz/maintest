using DownfallArena.Application.Accounts;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.Tests.Support;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>The operator's side of feedback (docs/tabletop/feedback-plan.md, R3): the status by hand, a kind moved, a sender muted.</summary>
public sealed class OperatorFeedbackTests : IDisposable
{
    private static readonly AccountId Mark = AccountId.New();

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly KeptFeedback _store = new();
    private readonly MemoryActivityStore _activity = new();
    private readonly FeedbackWrites _writes = new();
    private readonly FeedbackMutes _mutes;

    public OperatorFeedbackTests()
    {
        _mutes = new FeedbackMutes(new AccountActivityLog(_activity, _clock));
        _activity.Kept[Mark] = new AccountActivity(Mark, null, _clock.GetUtcNow());
    }

    public void Dispose() => _writes.Dispose();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private OperatorFeedback Desk() => new(_store, _mutes, _writes, _clock);

    private SendFeedback Sender() => new(_store, new GuestAddresses(), _writes, _clock, _mutes);

    private async Task<Feedback> KeptAsync(FeedbackKind kind = FeedbackKind.Bug, string sender = "seat-0123456789abcdef", string? account = null, string category = "looks")
    {
        var feedback = new Feedback
        {
            Id = Feedback.NewId(_clock.GetUtcNow()),
            Kind = kind,
            At = _clock.GetUtcNow(),
            Sender = sender,
            Account = account,
            Category = category,
            Text = "The bleed did not tick after the stun.",
            Expected = "One damage in the upkeep.",
            Context = new FeedbackContext { Table = "20261116-120000-deadbeef", Round = 3, SubPhase = "Upkeep" },
        };
        await _store.SaveAsync(feedback, Cancel);
        _clock.Advance(TimeSpan.FromSeconds(1));
        return feedback;
    }

    private static FeedbackSent Sent(string sender, string? account) => new()
    {
        Kind = FeedbackKind.Idea,
        Sender = sender,
        Account = account,
        Category = "battle",
        Text = "I wanted to see the last round again but could not.",
    };

    [Fact]
    public async Task A_bug_report_turned_into_an_idea_and_back_keeps_its_words_and_its_position()
    {
        var report = await KeptAsync(category: "rules");

        var idea = (await Desk().ChangeKindAsync(report.Id, FeedbackKind.Idea, Cancel)).Value;
        (idea.Kind, idea.Status, idea.Category, idea.Text, idea.Expected, idea.Context).ShouldBe((FeedbackKind.Idea, FeedbackStatus.Seen, "rules", report.Text, report.Expected, report.Context));

        var back = (await Desk().ChangeKindAsync(report.Id, FeedbackKind.Bug, Cancel)).Value;
        (back.Kind, back.Text, back.Expected, back.Context, back.Sender).ShouldBe((FeedbackKind.Bug, report.Text, report.Expected, report.Context, report.Sender));
        (await _store.FindAsync(report.Id, Cancel))!.Kind.ShouldBe(FeedbackKind.Bug);
    }

    [Fact]
    public async Task A_category_the_other_kind_does_not_have_becomes_something_else()
    {
        var report = await KeptAsync(category: "stuck");

        (await Desk().ChangeKindAsync(report.Id, FeedbackKind.Idea, Cancel)).Value.Category.ShouldBe("other");
    }

    [Fact]
    public async Task Closed_feedback_is_reopened_before_its_kind_changes()
    {
        var report = await KeptAsync();
        await Desk().SetStatusAsync(report.Id, FeedbackStatus.Dismissed, "Not a bug.", "mark", Cancel);

        var refused = await Desk().ChangeKindAsync(report.Id, FeedbackKind.Idea, Cancel);

        refused.Error.Code.ShouldBe("Feedback.Closed");
    }

    [Fact]
    public async Task Dismissing_says_why_and_closes_it_and_marking_it_seen_reopens_it()
    {
        var report = await KeptAsync();

        (await Desk().SetStatusAsync(report.Id, FeedbackStatus.Dismissed, "  ", "mark", Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        var dismissed = (await Desk().SetStatusAsync(report.Id, FeedbackStatus.Dismissed, "Works as the rules say.", "mark", Cancel)).Value.Feedback;
        (dismissed.Status, dismissed.Reason, dismissed.ClosedAt, dismissed.IsSpam).ShouldBe((FeedbackStatus.Dismissed, "Works as the rules say.", _clock.GetUtcNow(), false));

        var seen = (await Desk().SetStatusAsync(report.Id, FeedbackStatus.Seen, null, "mark", Cancel)).Value.Feedback;
        (seen.Status, seen.Reason, seen.ClosedAt, seen.IsClosed).ShouldBe((FeedbackStatus.Seen, null, null, false));
    }

    [Fact]
    public async Task An_idea_is_not_reproduced_and_no_status_is_set_on_feedback_not_kept()
    {
        var idea = await KeptAsync(FeedbackKind.Idea, category: "battle");

        (await Desk().SetStatusAsync(idea.Id, FeedbackStatus.Reproduced, null, "mark", Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        (await Desk().SetStatusAsync(Feedback.NewId(_clock.GetUtcNow()), FeedbackStatus.Seen, null, "mark", Cancel)).Error.Code.ShouldBe("Feedback.NotFound");
    }

    [Fact]
    public async Task A_muted_sender_is_refused_with_feedback_muted_before_any_other_guard()
    {
        var report = await KeptAsync();
        (await Desk().MuteAsync(report.Id, "mark", Cancel)).IsSuccess.ShouldBeTrue();

        var answer = await Sender().HandleAsync(Sent(report.Sender, null), Cancel);

        answer.Refused!.Code.ShouldBe("Feedback.Muted");
        (await Desk().UnmuteAsync(report.Id, Cancel)).IsSuccess.ShouldBeTrue();
        _clock.Advance(SendFeedback.Apart);
        (await Sender().HandleAsync(Sent(report.Sender, null), Cancel)).Refused.ShouldBeNull();
    }

    [Fact]
    public async Task A_mute_ends_on_its_own_after_thirty_days()
    {
        var report = await KeptAsync();
        await Desk().MuteAsync(report.Id, "mark", Cancel);

        _clock.Advance(FeedbackMute.Lasts - TimeSpan.FromMinutes(1));
        (await Sender().HandleAsync(Sent(report.Sender, null), Cancel)).Refused!.Code.ShouldBe("Feedback.Muted");
        _clock.Advance(TimeSpan.FromMinutes(1));
        (await Sender().HandleAsync(Sent(report.Sender, null), Cancel)).Refused.ShouldBeNull();
    }

    [Fact]
    public async Task Three_marked_as_spam_within_thirty_days_mute_their_sender()
    {
        var first = await KeptAsync();
        var second = await KeptAsync();
        var third = await KeptAsync();
        var other = await KeptAsync(sender: "seat-fedcba9876543210");

        (await Desk().SetStatusAsync(first.Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel)).Value.Muted.ShouldBeNull();
        (await Desk().SetStatusAsync(other.Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel)).Value.Muted.ShouldBeNull();
        _clock.Advance(TimeSpan.FromDays(10));
        (await Desk().SetStatusAsync(second.Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel)).Value.Muted.ShouldBeNull();
        _clock.Advance(TimeSpan.FromDays(10));
        var muted = (await Desk().SetStatusAsync(third.Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel)).Value.Muted.ShouldNotBeNull();

        (muted.Why, muted.Until).ShouldBe((FeedbackMute.BySpam, _clock.GetUtcNow() + FeedbackMute.Lasts));
        (await Desk().MuteOfAsync(first, Cancel)).ShouldBe(muted);
        (await Desk().MuteOfAsync(other, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task Spam_marks_older_than_thirty_days_do_not_count()
    {
        var first = await KeptAsync();
        var second = await KeptAsync();
        var third = await KeptAsync();
        await Desk().SetStatusAsync(first.Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel);
        _clock.Advance(TimeSpan.FromDays(31));
        await Desk().SetStatusAsync(second.Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel);

        (await Desk().SetStatusAsync(third.Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel)).Value.Muted.ShouldBeNull();
    }

    [Fact]
    public async Task An_account_s_mute_is_kept_with_its_activity_and_its_times_stay()
    {
        var idea = await KeptAsync(FeedbackKind.Idea, Mark.Value, Mark.Value, "battle");

        var mute = (await Desk().MuteAsync(idea.Id, "mark", Cancel)).Value;

        _activity.Kept[Mark].ShouldBe(new AccountActivity(Mark, null, new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero)) { FeedbackMute = mute });
        var anotherProcess = new FeedbackMutes(new AccountActivityLog(_activity, _clock));
        (await anotherProcess.FindAsync(Mark.Value, Mark.Value, _clock.GetUtcNow(), Cancel)).ShouldBe(mute);
    }

    [Fact]
    public async Task Being_seen_keeps_an_account_s_mute()
    {
        var idea = await KeptAsync(FeedbackKind.Idea, Mark.Value, Mark.Value, "battle");
        var mute = (await Desk().MuteAsync(idea.Id, "mark", Cancel)).Value;
        _clock.Advance(TimeSpan.FromHours(2));

        await new AccountActivityLog(_activity, _clock).SeenAsync(Mark, Cancel);

        _activity.Kept[Mark].FeedbackMute.ShouldBe(mute);
    }

    [Fact]
    public async Task A_deleted_account_s_feedback_has_nobody_to_mute()
    {
        var idea = await KeptAsync(FeedbackKind.Idea, ForgetFeedbackAccount.Gone, null, "battle");

        (await Desk().MuteAsync(idea.Id, "mark", Cancel)).Error.Code.ShouldBe("Feedback.NoSender");
    }

    [Fact]
    public async Task The_list_is_filtered_by_kind_newest_first_and_paged_twenty_at_a_time()
    {
        for (var index = 0; index < 22; index++)
        {
            await KeptAsync();
        }

        var idea = await KeptAsync(FeedbackKind.Idea, category: "battle");

        var bugs = await Desk().PageAsync(new FeedbackFilter(FeedbackKind.Bug), 2, Cancel);
        (bugs.Total, bugs.Pages, bugs.Page, bugs.Rows.Count).ShouldBe((22, 2, 2, 2));
        var ideas = await Desk().PageAsync(new FeedbackFilter(FeedbackKind.Idea), 1, Cancel);
        ideas.Rows.ShouldHaveSingleItem().Id.ShouldBe(idea.Id);
        (await Desk().PageAsync(new FeedbackFilter(), 1, Cancel)).Rows[0].Id.ShouldBe(idea.Id);
    }

    [Fact]
    public async Task The_overview_counts_what_is_new_and_the_bug_reports_among_it()
    {
        var seen = await KeptAsync();
        await KeptAsync();
        await KeptAsync(FeedbackKind.Idea, category: "battle");
        await Desk().SetStatusAsync(seen.Id, FeedbackStatus.Seen, null, "mark", Cancel);

        (await Desk().WaitingAsync(Cancel)).ShouldBe(new FeedbackWaiting(New: 2, NewBugs: 1, LastDay: 3, Full: false));
    }
}

using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.Tests.Support;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>The operator's side of feedback (docs/tabletop/feedback-plan.md, R3): the status by hand, a kind moved, a sender muted.</summary>
public sealed class OperatorDeskTests : IDisposable
{
    private static readonly AccountId Mark = AccountId.New();

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly KeptFeedback _store = new();
    private readonly MemoryActivityStore _activity = new();
    private readonly FeedbackWrites _writes = new();
    private readonly FeedbackMutes _mutes;

    public OperatorDeskTests()
    {
        _mutes = new FeedbackMutes(new AccountActivityLog(_activity, _clock));
        _activity.Kept[Mark] = new AccountActivity(Mark, null, _clock.GetUtcNow());
    }

    public void Dispose() => _writes.Dispose();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private OperatorDesk Desk() => new(_store, _mutes, _writes, _clock);

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
    public async Task A_bug_report_keeps_the_scenario_cut_from_it_and_its_status()
    {
        var report = await KeptAsync();

        var reproduced = await Desk().ReproducedAsync(report.Id, "sc-20261116-120000-0a1b2c3d", _ => Task.FromResult(false), Cancel);

        (reproduced.Value.Reproduction, reproduced.Value.Status).ShouldBe(("sc-20261116-120000-0a1b2c3d", FeedbackStatus.New));
        (await _store.FindAsync(report.Id, Cancel))!.Reproduction.ShouldBe("sc-20261116-120000-0a1b2c3d");
    }

    [Fact]
    public async Task A_scenario_another_press_kept_stays_and_one_since_removed_is_replaced()
    {
        var report = await KeptAsync();
        await Desk().ReproducedAsync(report.Id, "sc-20261116-120000-00000001", _ => Task.FromResult(false), Cancel);

        var second = await Desk().ReproducedAsync(report.Id, "sc-20261116-120000-00000002", _ => Task.FromResult(true), Cancel);
        var third = await Desk().ReproducedAsync(report.Id, "sc-20261116-120000-00000003", _ => Task.FromResult(false), Cancel);

        (second.Value.Reproduction, third.Value.Reproduction).ShouldBe(("sc-20261116-120000-00000001", "sc-20261116-120000-00000003"));
    }

    [Fact]
    public async Task An_idea_is_not_reproduced()
    {
        var idea = await KeptAsync(FeedbackKind.Idea, category: "battle");

        var refused = await Desk().ReproducedAsync(idea.Id, "sc-20261116-120000-0a1b2c3d", _ => Task.FromResult(false), Cancel);

        refused.Error.ShouldBe(FeedbackErrors.NotABug);
    }

    [Fact]
    public async Task A_bug_report_turned_into_an_idea_and_back_keeps_its_words_and_its_position()
    {
        var report = await KeptAsync(category: "rules");

        var idea = (await Desk().ChangeKindAsync(report.Id, FeedbackKind.Idea, Cancel)).Value.Feedback;
        (idea.Kind, idea.Status, idea.Category, idea.Text, idea.Expected, idea.Context).ShouldBe((FeedbackKind.Idea, FeedbackStatus.Seen, "rules", report.Text, report.Expected, report.Context));

        var back = (await Desk().ChangeKindAsync(report.Id, FeedbackKind.Bug, Cancel)).Value.Feedback;
        (back.Kind, back.Text, back.Expected, back.Context, back.Sender).ShouldBe((FeedbackKind.Bug, report.Text, report.Expected, report.Context, report.Sender));
        (await _store.FindAsync(report.Id, Cancel))!.Kind.ShouldBe(FeedbackKind.Bug);
    }

    [Fact]
    public async Task A_category_the_other_kind_does_not_have_becomes_something_else()
    {
        var report = await KeptAsync(category: "stuck");

        (await Desk().ChangeKindAsync(report.Id, FeedbackKind.Idea, Cancel)).Value.Feedback.Category.ShouldBe("other");
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
        (await Desk().UnmuteAsync(report.Id, Cancel)).Value.Changed.ShouldBeTrue();
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

        var mute = (await Desk().MuteAsync(idea.Id, "mark", Cancel)).Value.Mute;

        _activity.Kept[Mark].ShouldBe(new AccountActivity(Mark, null, new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero)) { FeedbackMute = mute });
        var anotherProcess = new FeedbackMutes(new AccountActivityLog(_activity, _clock));
        (await anotherProcess.FindAsync(Mark.Value, Mark.Value, _clock.GetUtcNow(), Cancel)).ShouldBe(mute);
    }

    [Fact]
    public async Task Being_seen_keeps_an_account_s_mute()
    {
        var idea = await KeptAsync(FeedbackKind.Idea, Mark.Value, Mark.Value, "battle");
        var mute = (await Desk().MuteAsync(idea.Id, "mark", Cancel)).Value.Mute;
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

    [Fact]
    public async Task A_status_asked_again_changes_nothing_and_keeps_when_it_was_closed()
    {
        var report = await KeptAsync();
        var first = (await Desk().SetStatusAsync(report.Id, FeedbackStatus.Dismissed, "Works as the rules say.", "mark", Cancel)).Value;
        _clock.Advance(TimeSpan.FromDays(3));

        var again = (await Desk().SetStatusAsync(report.Id, FeedbackStatus.Dismissed, "Works as the rules say.", "mark", Cancel)).Value;
        var otherReason = (await Desk().SetStatusAsync(report.Id, FeedbackStatus.Dismissed, "A duplicate.", "mark", Cancel)).Value;

        (first.Changed, again.Changed, otherReason.Changed).ShouldBe((true, false, true));
        otherReason.Feedback.ClosedAt.ShouldBe(first.Feedback.ClosedAt, "the 180 days run from the first time it was closed");
    }

    [Fact]
    public async Task A_spam_mark_asked_again_does_not_mute_a_sender_the_operator_let_through()
    {
        var marked = new List<Feedback> { await KeptAsync(), await KeptAsync(), await KeptAsync() };
        foreach (var one in marked)
        {
            await Desk().SetStatusAsync(one.Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel);
        }

        await Desk().UnmuteAsync(marked[0].Id, Cancel);

        (await Desk().SetStatusAsync(marked[2].Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel)).Value.ShouldBe(
            new FeedbackStatusChange((await _store.FindAsync(marked[2].Id, Cancel))!, Changed: false, Muted: null));
        (await Desk().MuteOfAsync(marked[0], Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_dismissal_s_reason_is_one_line()
    {
        var report = await KeptAsync();

        (await Desk().SetStatusAsync(report.Id, FeedbackStatus.Dismissed, "Spam.\n  Feedback marked seen by someone else.", "mark", Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
    }

    [Fact]
    public async Task Muting_again_keeps_the_mute_that_holds_and_lifting_none_changes_nothing()
    {
        var report = await KeptAsync();

        (await Desk().UnmuteAsync(report.Id, Cancel)).Value.Changed.ShouldBeFalse();
        var first = (await Desk().MuteAsync(report.Id, "mark", Cancel)).Value;
        _clock.Advance(TimeSpan.FromHours(1));
        var again = (await Desk().MuteAsync(report.Id, "ops", Cancel)).Value;

        (first.Changed, again.Changed, again.Mute).ShouldBe((true, false, first.Mute));
    }

    [Fact]
    public async Task An_account_none_of_whose_activity_is_kept_yet_is_muted_all_the_same()
    {
        var stranger = AccountId.New();
        var idea = await KeptAsync(FeedbackKind.Idea, stranger.Value, stranger.Value, "battle");

        var mute = (await Desk().MuteAsync(idea.Id, "mark", Cancel)).Value.Mute;

        _activity.Kept[stranger].ShouldBe(new AccountActivity(stranger, null, null) { FeedbackMute = mute });
    }

    [Fact]
    public async Task An_account_s_mute_is_dropped_at_the_first_write_after_it_ends()
    {
        var idea = await KeptAsync(FeedbackKind.Idea, Mark.Value, Mark.Value, "battle");
        await Desk().MuteAsync(idea.Id, "mark", Cancel);
        _clock.Advance(FeedbackMute.Lasts);

        await new AccountActivityLog(_activity, _clock).SeenAsync(Mark, Cancel);

        _activity.Kept[Mark].FeedbackMute.ShouldBeNull();
    }

    [Fact]
    public async Task On_a_host_that_keeps_no_account_activity_an_account_s_sender_cannot_be_muted()
    {
        var idea = await KeptAsync(FeedbackKind.Idea, Mark.Value, Mark.Value, "battle");
        var desk = new OperatorDesk(_store, new FeedbackMutes(null), _writes, _clock);

        desk.CanMute(idea).ShouldBeFalse();
        (await desk.MuteAsync(idea.Id, "mark", Cancel)).Error.Code.ShouldBe("Feedback.NoSender");
    }

    [Fact]
    public async Task The_host_is_full_at_a_hundred_sent_in_a_day()
    {
        for (var index = 0; index < SendFeedback.PerHost; index++)
        {
            await KeptAsync();
        }

        (await Desk().WaitingAsync(Cancel)).Full.ShouldBeTrue();
    }

    [Fact]
    public async Task A_third_spam_mark_whose_mute_could_not_be_written_mutes_when_asked_again()
    {
        var failing = new FailsOnce(_activity);
        var desk = new OperatorDesk(_store, new FeedbackMutes(new AccountActivityLog(failing, _clock)), _writes, _clock);
        var marked = new List<Feedback>();
        for (var index = 0; index < FeedbackMute.SpamMarks; index++)
        {
            marked.Add(await KeptAsync(FeedbackKind.Idea, Mark.Value, Mark.Value, "battle"));
        }

        await desk.SetStatusAsync(marked[0].Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel);
        await desk.SetStatusAsync(marked[1].Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel);
        failing.Armed = true;
        await Should.ThrowAsync<IOException>(() => desk.SetStatusAsync(marked[2].Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel));

        var again = (await desk.SetStatusAsync(marked[2].Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel)).Value;

        (again.Changed, again.Muted?.Why, again.Feedback.SpamCounted).ShouldBe((false, FeedbackMute.BySpam, true));
    }

    [Fact]
    public async Task A_bug_report_is_filed_with_its_issue_s_number_and_filed_again_under_the_same_changes_nothing()
    {
        var report = await KeptAsync();

        var filed = (await Desk().FileAsync(report.Id, 731, Cancel)).Value;
        var again = (await Desk().FileAsync(report.Id, 731, Cancel)).Value;

        (filed.Changed, filed.Feedback.Status, filed.Feedback.Issue, filed.Feedback.IsClosed).ShouldBe((true, FeedbackStatus.Filed, 731, false));
        again.Changed.ShouldBeFalse();
        (await _store.FindAsync(report.Id, Cancel))!.Issue.ShouldBe(731);
    }

    [Fact]
    public async Task Another_number_replaces_a_mistyped_one()
    {
        var report = await KeptAsync();
        await Desk().FileAsync(report.Id, 713, Cancel);

        var filed = (await Desk().FileAsync(report.Id, 731, Cancel)).Value;

        (filed.Changed, filed.Feedback.Issue).ShouldBe((true, 731));
    }

    [Fact]
    public async Task Filing_names_a_number_and_an_idea_is_not_filed()
    {
        var report = await KeptAsync();
        var idea = await KeptAsync(FeedbackKind.Idea, category: "battle");

        (await Desk().FileAsync(report.Id, null, Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        (await Desk().FileAsync(report.Id, 0, Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
        (await Desk().FileAsync(idea.Id, 731, Cancel)).Error.Code.ShouldBe("Feedback.Invalid");
    }

    [Fact]
    public async Task A_filed_report_is_fixed_by_its_issue_and_closed_and_one_never_filed_is_not()
    {
        var filed = await KeptAsync();
        var unfiled = await KeptAsync();
        await Desk().FileAsync(filed.Id, 731, Cancel);

        var fixedReport = (await Desk().SetStatusAsync(filed.Id, FeedbackStatus.Fixed, null, "mark", Cancel)).Value.Feedback;
        var refused = await Desk().SetStatusAsync(unfiled.Id, FeedbackStatus.Fixed, null, "mark", Cancel);

        (fixedReport.Status, fixedReport.Issue, fixedReport.IsClosed, fixedReport.ClosedAt).ShouldBe((FeedbackStatus.Fixed, 731, true, _clock.GetUtcNow()));
        refused.Error.Code.ShouldBe("Feedback.NotFiled");
    }

    /// <summary>An activity store whose next write fails once it is armed, as a storage hiccup would.</summary>
    private sealed class FailsOnce(IAccountActivityStore inner) : IAccountActivityStore
    {
        public bool Armed { get; set; }

        public Task<AccountActivity?> FindAsync(AccountId account, CancellationToken cancellationToken = default) => inner.FindAsync(account, cancellationToken);

        public Task<IReadOnlyList<AccountActivity>> ListAsync(CancellationToken cancellationToken = default) => inner.ListAsync(cancellationToken);

        public Task SaveAsync(AccountActivity activity, CancellationToken cancellationToken = default)
        {
            if (Armed)
            {
                Armed = false;
                throw new IOException("The storage did not answer.");
            }

            return inner.SaveAsync(activity, cancellationToken);
        }

        public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) => inner.ForgetAsync(account, cancellationToken);
    }
}

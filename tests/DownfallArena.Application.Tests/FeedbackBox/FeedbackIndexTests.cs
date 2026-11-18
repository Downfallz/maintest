using DownfallArena.Application.Accounts;
using DownfallArena.Application.FeedbackBox;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>
/// The index of feedback (#866): the operator's list, the Overview's count and the spam count read it in place of every
/// record, it is written with each change, and a listing of the store tells it what another process saved or let go of.
/// </summary>
public sealed class FeedbackIndexTests : IDisposable
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly KeptFeedback _store = new();
    private readonly FeedbackWrites _writes = new();
    private readonly FeedbackIndex _index;

    public FeedbackIndexTests()
    {
        _index = new FeedbackIndex(_store, _store, _clock);
    }

    public void Dispose() => _writes.Dispose();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private OperatorDesk Desk() => new(_index, new FeedbackMutes(null), _writes, _clock);

    private Feedback Sent(string sender, FeedbackKind kind = FeedbackKind.Bug)
    {
        var at = _clock.GetUtcNow();
        _clock.Advance(TimeSpan.FromMinutes(1));
        return new Feedback { Id = Feedback.NewId(at), Kind = kind, At = at, Sender = sender, Category = kind == FeedbackKind.Bug ? "rules" : "battle", Text = "The bleed did not tick after the stun." };
    }

    /// <summary>Keeps feedback as the host keeps it: through the index's store, so the index is written with it.</summary>
    private async Task<Feedback> KeptAsync(string sender, FeedbackKind kind = FeedbackKind.Bug)
    {
        var sent = Sent(sender, kind);
        await _index.Records.SaveAsync(sent, Cancel);
        return sent;
    }

    [Fact]
    public async Task Once_built_the_list_opens_the_page_s_rows_alone_and_the_overview_s_count_opens_none()
    {
        for (var one = 0; one < 30; one++)
        {
            await KeptAsync($"seat-{one}");
        }

        await Desk().PageAsync(new FeedbackFilter(OpenOnly: true), 1, Cancel);
        var opened = _store.Opened;
        var page = await Desk().PageAsync(new FeedbackFilter(OpenOnly: true), 2, Cancel);
        var listing = _store.Opened - opened;
        var waiting = await Desk().WaitingAsync(Cancel);

        (page.Total, page.Rows.Count, listing).ShouldBe((30, 10, 10));
        waiting.New.ShouldBe(30);
        (_store.Opened - opened).ShouldBe(10, "the Overview's count reads the index alone");
    }

    [Fact]
    public async Task Feedback_saved_or_let_go_of_by_another_process_is_found_by_the_store_s_listing()
    {
        var kept = await KeptAsync("seat-a");
        var gone = await KeptAsync("seat-b");
        await Desk().WaitingAsync(Cancel);
        var other = Sent("seat-c");

        await _store.SaveAsync(other, Cancel);
        await _store.DeleteAsync(gone.Id, Cancel);

        (await Desk().PageAsync(new FeedbackFilter(OpenOnly: true), 1, Cancel)).Rows.Select(one => one.Id).ShouldBe([other.Id, kept.Id]);
        _store.Index.ShouldNotBeNull().Rows.Select(row => row.Id).ShouldBe([kept.Id, other.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Closed_feedback_is_kept_by_its_id_and_listed_when_the_filter_asks_for_it()
    {
        var open = await KeptAsync("seat-a");
        var closed = await KeptAsync("seat-b");

        await Desk().SetStatusAsync(closed.Id, FeedbackStatus.Dismissed, "not a bug", "mark", Cancel);

        _store.Index.ShouldNotBeNull().Rows.Select(row => row.Id).ShouldBe([open.Id]);
        _store.Index!.Closed.ShouldBe([closed.Id]);
        (await Desk().PageAsync(new FeedbackFilter(OpenOnly: true), 1, Cancel)).Rows.Select(one => one.Id).ShouldBe([open.Id]);
        (await Desk().PageAsync(new FeedbackFilter(Status: FeedbackStatus.Dismissed), 1, Cancel)).Rows.Select(one => one.Id).ShouldBe([closed.Id]);
    }

    [Fact]
    public async Task The_third_spam_mark_mutes_its_sender_from_the_index_alone()
    {
        var marks = new List<Feedback>();
        for (var one = 0; one < FeedbackMute.SpamMarks; one++)
        {
            marks.Add(await KeptAsync("seat-flood"));
        }

        await Desk().SetStatusAsync(marks[0].Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel);
        await Desk().SetStatusAsync(marks[1].Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel);
        var opened = _store.Opened;
        var third = await Desk().SetStatusAsync(marks[2].Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel);

        third.Value.Muted.ShouldNotBeNull();
        (_store.Opened - opened).ShouldBeLessThanOrEqualTo(3, "the mark reads its own record, never the others");
    }

    [Fact]
    public async Task A_spam_mark_older_than_the_window_leaves_the_rows_and_counts_no_more()
    {
        var mark = await KeptAsync("seat-flood");
        await Desk().SetStatusAsync(mark.Id, FeedbackStatus.Dismissed, Feedback.SpamReason, "mark", Cancel);
        _store.Index!.Rows.ShouldHaveSingleItem().Id.ShouldBe(mark.Id);

        _clock.Advance(FeedbackMute.SpamWindow + TimeSpan.FromDays(1));
        await KeptAsync("seat-a");

        _store.Index!.Closed.ShouldBe([mark.Id]);
    }

    [Fact]
    public async Task A_change_written_by_another_process_first_is_read_again_and_neither_is_lost()
    {
        var first = await KeptAsync("seat-a");
        var second = Sent("seat-b");
        var elsewhere = Sent("seat-c");
        _store.BeforeIndexWrite = async () =>
        {
            await _store.SaveAsync(elsewhere, Cancel);
            await new FeedbackIndex(_store, _store, _clock).Records.SaveAsync(elsewhere, Cancel);
        };

        await _index.Records.SaveAsync(second, Cancel);

        _store.Index!.Rows.Select(row => row.Id).ShouldBe([first.Id, second.Id, elsewhere.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task A_row_the_index_holds_wrong_is_put_right_when_the_list_opens_it()
    {
        var report = await KeptAsync("seat-a");
        await _store.SaveAsync(report with { Status = FeedbackStatus.Seen }, Cancel);

        var page = await Desk().PageAsync(new FeedbackFilter(OpenOnly: true), 1, Cancel);

        page.Rows.ShouldHaveSingleItem().Status.ShouldBe(FeedbackStatus.Seen);
        _store.Index!.Rows.ShouldHaveSingleItem().Status.ShouldBe(FeedbackStatus.Seen);
        (await Desk().WaitingAsync(Cancel)).New.ShouldBe(0);
    }
}

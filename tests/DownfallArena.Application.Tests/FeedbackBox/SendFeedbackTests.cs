using DownfallArena.Application.FeedbackBox;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>The guards against spam (docs/tabletop/feedback-plan.md, "Against spam"), over bug reports and ideas together.</summary>
public sealed class SendFeedbackTests : IDisposable
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly KeptFeedback _store = new();
    private readonly GuestAddresses _addresses = new();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly FeedbackWrites _writes = new();

    private SendFeedback Desk() => new(_store, _addresses, _writes, _clock);

    public void Dispose() => _writes.Dispose();

    private static FeedbackSent Report(string sender = "acct_1", int round = 3, string? account = "acct_1", string? address = null) => new()
    {
        Kind = FeedbackKind.Bug,
        Sender = sender,
        Account = account,
        Address = address,
        Category = "rules",
        Text = "The bleed did not tick after the stun.",
        Context = new FeedbackContext { Table = "20261116-120000-deadbeef", Round = round },
    };

    private static FeedbackSent Idea(string sender = "acct_1") => new()
    {
        Kind = FeedbackKind.Idea,
        Sender = sender,
        Account = sender,
        Category = "battle",
        Text = "I wanted to see the last round again but could not.",
    };

    /// <summary>Sends, then moves the clock past the two minutes between two pieces of feedback.</summary>
    private async Task<FeedbackAnswer> SentAsync(FeedbackSent sent)
    {
        var answer = await Desk().HandleAsync(sent, Cancel);
        _clock.Advance(SendFeedback.Apart);
        return answer;
    }

    [Fact]
    public async Task A_report_is_kept_new_with_its_words_and_context()
    {
        var answer = await Desk().HandleAsync(Report() with { Expected = "  One damage.  " }, Cancel);

        answer.Refused.ShouldBeNull();
        var kept = _store.All.Single();
        (kept.Id, kept.Status, kept.Kind, kept.Expected, kept.Context!.Round).ShouldBe((answer.Id, FeedbackStatus.New, FeedbackKind.Bug, "One damage.", 3));
    }

    [Theory]
    [InlineData("rules", "too short")]
    [InlineData("weather", "The bleed did not tick after the stun.")]
    public async Task A_report_whose_category_or_text_is_out_of_bounds_is_refused(string category, string text)
    {
        var answer = await Desk().HandleAsync(Report() with { Category = category, Text = text }, Cancel);

        answer.Refused!.Code.ShouldBe("Feedback.Invalid");
        _store.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task Feedback_sent_within_two_minutes_of_the_last_is_refused_with_the_seconds_left()
    {
        await Desk().HandleAsync(Report(round: 1), Cancel);
        _clock.Advance(TimeSpan.FromSeconds(30));

        var answer = await Desk().HandleAsync(Idea(), Cancel);

        answer.Refused!.Code.ShouldBe("Feedback.TooSoon");
        answer.RetryAfter.ShouldBe(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public async Task A_sixth_piece_of_feedback_in_a_day_is_refused_reports_and_ideas_together()
    {
        for (var round = 1; round <= 4; round++)
        {
            await SentAsync(Report(round: round));
        }

        await SentAsync(Idea());

        (await Desk().HandleAsync(Report(round: 9), Cancel)).Refused!.Code.ShouldBe("Feedback.TooMany");
    }

    [Fact]
    public async Task The_day_is_counted_from_now_so_a_sender_can_send_again_a_day_later()
    {
        for (var round = 1; round <= 5; round++)
        {
            await SentAsync(Report(round: round));
        }

        _clock.Advance(SendFeedback.Day);

        (await Desk().HandleAsync(Report(round: 9), Cancel)).Refused.ShouldBeNull();
    }

    [Fact]
    public async Task A_guest_on_new_seats_is_still_counted_against_its_address()
    {
        for (var seat = 1; seat <= 5; seat++)
        {
            await SentAsync(Report(sender: $"seat-{seat}", account: null, address: "203.0.113.7"));
        }

        var sixth = await Desk().HandleAsync(Report(sender: "seat-6", account: null, address: "203.0.113.7"), Cancel);
        var elsewhere = await Desk().HandleAsync(Report(sender: "seat-7", account: null, address: "198.51.100.2"), Cancel);

        (sixth.Refused!.Code, elsewhere.Refused).ShouldBe(("Feedback.TooMany", null));
    }

    [Fact]
    public async Task A_signed_in_player_is_not_counted_by_address()
    {
        for (var sender = 1; sender <= 5; sender++)
        {
            await SentAsync(Report(sender: $"acct_{sender}", account: $"acct_{sender}", address: "203.0.113.7"));
        }

        (await Desk().HandleAsync(Report(sender: "acct_6", account: "acct_6", address: "203.0.113.7"), Cancel)).Refused.ShouldBeNull();
    }

    [Fact]
    public async Task A_second_report_on_the_same_table_and_round_is_merged_into_the_first()
    {
        var first = await SentAsync(Report());

        var second = await Desk().HandleAsync(Report() with { Text = "It happened again on the same round." }, Cancel);

        (second.Id, second.Merged).ShouldBe((first.Id, true));
        var kept = _store.All.Single();
        kept.Additions.Single().Text.ShouldBe("It happened again on the same round.");
    }

    [Fact]
    public async Task A_second_idea_within_the_hour_is_merged_and_one_after_it_is_not()
    {
        var first = await SentAsync(Idea());
        var merged = await SentAsync(Idea());
        _clock.Advance(SendFeedback.IdeaMerged);

        var later = await Desk().HandleAsync(Idea(), Cancel);

        (merged.Id, merged.Merged).ShouldBe((first.Id, true));
        (later.Merged, later.Id == first.Id).ShouldBe((false, false));
    }

    [Fact]
    public async Task Past_a_hundred_new_pieces_in_a_day_the_host_takes_no_more()
    {
        for (var sender = 0; sender < SendFeedback.PerHost; sender++)
        {
            await Desk().HandleAsync(Report(sender: $"acct_{sender}", account: $"acct_{sender}"), Cancel);
            _clock.Advance(TimeSpan.FromMilliseconds(10));
        }

        (await Desk().HandleAsync(Report(sender: "acct_late", account: "acct_late"), Cancel)).Refused!.Code.ShouldBe("Feedback.HostFull");
    }

    [Fact]
    public async Task The_page_errors_kept_are_its_last_ten()
    {
        var errors = Enumerable.Range(1, 12).Select(index => new PageError($"Error {index}", $"battle.js:{index}")).ToList();

        await Desk().HandleAsync(Report() with { Errors = errors }, Cancel);

        _store.All.Single().Errors.Select(error => error.Message).ShouldBe(Enumerable.Range(3, 10).Select(index => $"Error {index}"));
    }

    [Fact]
    public async Task The_same_words_sent_again_after_a_lost_answer_are_told_they_were_kept()
    {
        var first = await Desk().HandleAsync(Report(), Cancel);
        _clock.Advance(TimeSpan.FromSeconds(15));

        var again = await Desk().HandleAsync(Report(), Cancel);

        (again.Refused, again.Id).ShouldBe((null, first.Id));
        _store.All.Single().Additions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Text_added_late_to_yesterday_s_report_still_counts_within_the_day()
    {
        var first = await SentAsync(Report());
        _clock.Advance(SendFeedback.Day - SendFeedback.Apart - TimeSpan.FromMinutes(1));
        var merged = await Desk().HandleAsync(Report() with { Text = "Still happens on this round." }, Cancel);
        _clock.Advance(TimeSpan.FromSeconds(90));

        var soon = await Desk().HandleAsync(Report(round: 9) with { Text = "A different problem on another round." }, Cancel);

        (merged.Id, soon.Refused!.Code).ShouldBe((first.Id, "Feedback.TooSoon"));
    }

    [Fact]
    public async Task Too_many_is_refused_until_the_oldest_counted_one_leaves_the_day()
    {
        for (var round = 1; round <= 5; round++)
        {
            await SentAsync(Report(round: round));
        }

        var refused = await Desk().HandleAsync(Report(round: 9), Cancel);

        refused.RetryAfter.ShouldBe(SendFeedback.Day - (SendFeedback.Apart * 5));
    }

    [Fact]
    public async Task A_browser_named_at_length_is_cut_not_refused()
    {
        var answer = await Desk().HandleAsync(Report() with { Browser = new string('b', 900) }, Cancel);

        answer.Refused.ShouldBeNull();
        _store.All.Single().Browser!.Length.ShouldBe(500);
    }

    [Fact]
    public async Task A_burst_sent_at_once_keeps_one_and_refuses_the_rest()
    {
        var desk = Desk();

        var answers = await Task.WhenAll(Enumerable.Range(1, 20).Select(round => Task.Run(() => desk.HandleAsync(Report(round: round) with { Text = $"The bleed did not tick, try {round}." }, Cancel), Cancel)));

        answers.Count(answer => answer.Refused is null).ShouldBe(1);
        _store.All.Count.ShouldBe(1);
    }
}

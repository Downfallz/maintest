using System.Text.Json;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Infrastructure.Tests.FeedbackBox;

/// <summary>What every feedback store promises (ADR 0189), played against each adapter.</summary>
public abstract class FeedbackStoreContract
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<IFeedbackStore> StoreAsync();

    private static Feedback Report(DateTimeOffset at) => new()
    {
        Id = Feedback.NewId(at),
        Kind = FeedbackKind.Bug,
        At = at,
        Sender = "acct_0000000000000001",
        Account = "acct_0000000000000001",
        Category = "rules",
        Text = "The bleed did not tick after the stun.",
        Expected = "One damage in the upkeep.",
        Page = "https://play.example/battle",
        Errors = [new PageError("TypeError: x is undefined", "battle.js:12")],
        Context = new FeedbackContext
        {
            Table = "20261116-030000-deadbeef",
            Seat = "player1",
            Round = 3,
            SubPhase = "Upkeep",
            JournalLength = 14,
            Position = JsonSerializer.SerializeToElement(new { round = 3, creatures = new[] { 1, 2 } }),
        },
        Additions = [new FeedbackAddition(at.AddMinutes(5), "Again on the same round.", null)],
    };

    private static string Json(Feedback? feedback) => JsonSerializer.Serialize(feedback);

    [Fact]
    public async Task Feedback_reads_back_as_it_was_kept()
    {
        var store = await StoreAsync();
        var report = Report(Start);

        await store.SaveAsync(report, Cancel);

        Json(await store.FindAsync(report.Id, Cancel)).ShouldBe(Json(report));
    }

    [Fact]
    public async Task Feedback_saved_again_under_its_id_replaces_what_was_kept()
    {
        var store = await StoreAsync();
        var report = Report(Start);
        await store.SaveAsync(report, Cancel);

        await store.SaveAsync(report with { Status = FeedbackStatus.Seen }, Cancel);

        (await store.FindAsync(report.Id, Cancel))!.Status.ShouldBe(FeedbackStatus.Seen);
        (await store.IdsAsync(Cancel)).ShouldBe([report.Id]);
    }

    [Fact]
    public async Task Ids_are_listed_newest_first()
    {
        var store = await StoreAsync();
        Feedback[] sent = [Report(Start.AddMinutes(1)), Report(Start), Report(Start.AddMinutes(2))];
        foreach (var feedback in sent)
        {
            await store.SaveAsync(feedback, Cancel);
        }

        (await store.IdsAsync(Cancel)).ShouldBe([sent[2].Id, sent[0].Id, sent[1].Id]);
    }

    [Fact]
    public async Task Ids_sent_within_a_span_are_listed_newest_first_and_none_outside_it()
    {
        var store = await StoreAsync();
        var after = Start.AddDays(-1);
        Feedback[] inside = [Report(after.AddMinutes(1)), Report(Start.AddHours(-1)), Report(Start)];
        Feedback[] outside = [Report(Start.AddDays(-3)), Report(after), Report(Start.AddMinutes(1)), Report(Start.AddDays(2))];
        foreach (var feedback in inside.Concat(outside))
        {
            await store.SaveAsync(feedback, Cancel);
        }

        (await store.IdsSentAsync(after, Start, Cancel)).ShouldBe([inside[2].Id, inside[1].Id, inside[0].Id]);
    }

    [Fact]
    public async Task Ids_sent_across_the_turn_of_a_year_are_listed_from_both_years()
    {
        var store = await StoreAsync();
        var newYear = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Feedback[] sent = [Report(newYear.AddHours(-2)), Report(newYear.AddHours(1)), Report(newYear.AddDays(-40))];
        foreach (var feedback in sent)
        {
            await store.SaveAsync(feedback, Cancel);
        }

        (await store.IdsSentAsync(newYear.AddDays(-2), newYear.AddHours(2), Cancel)).ShouldBe([sent[1].Id, sent[0].Id]);
        (await store.IdsSentAsync(newYear, newYear.AddDays(-1), Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleted_feedback_is_no_longer_found_or_listed()
    {
        var store = await StoreAsync();
        var report = Report(Start);
        await store.SaveAsync(report, Cancel);

        await store.DeleteAsync(report.Id, Cancel);
        await store.DeleteAsync(report.Id, Cancel);

        (await store.FindAsync(report.Id, Cancel)).ShouldBeNull();
        (await store.IdsAsync(Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_store_with_nothing_kept_lists_nothing_and_finds_nothing_under_an_id_it_never_made()
    {
        var store = await StoreAsync();

        (await store.IdsAsync(Cancel)).ShouldBeEmpty();
        (await store.IdsSentAsync(Start.AddDays(-2), Start, Cancel)).ShouldBeEmpty();
        (await store.FindAsync(Feedback.NewId(Start), Cancel)).ShouldBeNull();
        (await store.FindAsync("../escape", Cancel)).ShouldBeNull();
    }
}

using DownfallArena.Application.FeedbackBox;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>A deleted account's feedback keeps its text and loses the account (ADR 0189).</summary>
public sealed class ForgetFeedbackAccountTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static Feedback Sent(string account, int minute) => new()
    {
        Id = Feedback.NewId(new DateTimeOffset(2026, 11, 16, 12, minute, 0, TimeSpan.Zero)),
        Kind = FeedbackKind.Idea,
        At = new DateTimeOffset(2026, 11, 16, 12, minute, 0, TimeSpan.Zero),
        Sender = account,
        Account = account,
        Category = "lobby",
        Text = "I wanted to invite a friend by name.",
    };

    [Fact]
    public async Task The_accounts_feedback_keeps_its_text_and_names_nobody()
    {
        var store = new KeptFeedback();
        await store.SaveAsync(Sent("acct_gone", 1), Cancel);
        await store.SaveAsync(Sent("acct_kept", 2), Cancel);

        using var writes = new FeedbackWrites();
        var forgotten = await new ForgetFeedbackAccount(store, writes).HandleAsync("acct_gone", Cancel);

        forgotten.ShouldBe(1);
        var gone = store.All.Single(one => one.Text.Length > 0 && one.Account is null);
        (gone.Sender, gone.Text).ShouldBe((ForgetFeedbackAccount.Gone, "I wanted to invite a friend by name."));
        store.All.Count(one => one.Account == "acct_kept").ShouldBe(1);
    }
}

using DownfallArena.Application.Accounts;
using DownfallArena.Application.FeedbackBox;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>The question a page asks before its player writes (#717): whether the seat or account is muted now (#865).</summary>
public sealed class AskFeedbackMutedTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly FeedbackMutes _mutes = new(null);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private AskFeedbackMuted Ask() => new(_mutes, _clock);

    [Fact]
    public async Task A_guest_the_operator_muted_is_muted_and_another_is_not()
    {
        await _mutes.SetAsync("seat-a", null, FeedbackMute.Begun(_clock.GetUtcNow(), "op", "noise"), Cancel);

        (await Ask().HandleAsync("seat-a", null, Cancel), await Ask().HandleAsync("seat-b", null, Cancel)).ShouldBe((true, false));
    }

    [Fact]
    public async Task A_mute_that_ended_mutes_no_more()
    {
        await _mutes.SetAsync("seat-a", null, FeedbackMute.Begun(_clock.GetUtcNow(), "op", "noise"), Cancel);

        _clock.Advance(FeedbackMute.Lasts + TimeSpan.FromMinutes(1));

        (await Ask().HandleAsync("seat-a", null, Cancel)).ShouldBeFalse();
    }
}

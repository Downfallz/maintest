using DownfallArena.Application.Accounts;
using DownfallArena.Application.Tests.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Accounts;

public sealed class AccountActivityLogTests
{
    private static readonly AccountId Mark = AccountId.New();

    private readonly MemoryActivityStore _store = new();
    private readonly MovableClock _clock = new(FixedTimeProvider.Default);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private AccountActivityLog Log() => new(_store, _clock);

    [Fact]
    public async Task A_sign_in_is_kept_as_the_last_sign_in_and_the_last_visit()
    {
        await Log().SignedInAsync(Mark, Cancel);

        _store.Kept[Mark].ShouldBe(new AccountActivity(Mark, FixedTimeProvider.Default, FixedTimeProvider.Default));
    }

    [Fact]
    public async Task Being_seen_keeps_the_last_sign_in()
    {
        var log = Log();
        await log.SignedInAsync(Mark, Cancel);
        _clock.Advance(TimeSpan.FromHours(2));

        await log.SeenAsync(Mark, Cancel);

        _store.Kept[Mark].ShouldBe(new AccountActivity(Mark, FixedTimeProvider.Default, FixedTimeProvider.Default.AddHours(2)));
    }

    [Fact]
    public async Task Being_seen_again_within_the_hour_writes_nothing()
    {
        var log = Log();
        await log.SeenAsync(Mark, Cancel);
        _clock.Advance(TimeSpan.FromMinutes(59));

        await log.SeenAsync(Mark, Cancel);

        _store.Writes.ShouldBe(1);
        _store.Kept[Mark].LastSeen.ShouldBe(FixedTimeProvider.Default);
    }

    [Fact]
    public async Task Being_seen_an_hour_later_is_written()
    {
        var log = Log();
        await log.SeenAsync(Mark, Cancel);
        _clock.Advance(AccountActivityLog.SeenEvery);

        await log.SeenAsync(Mark, Cancel);

        _store.Writes.ShouldBe(2);
        _store.Kept[Mark].LastSeen.ShouldBe(FixedTimeProvider.Default + AccountActivityLog.SeenEvery);
    }

    [Fact]
    public async Task A_visit_another_process_kept_within_the_hour_is_not_written_again()
    {
        await Log().SeenAsync(Mark, Cancel);
        _clock.Advance(TimeSpan.FromMinutes(10));

        await Log().SeenAsync(Mark, Cancel);

        _store.Writes.ShouldBe(1);
    }

    [Fact]
    public async Task A_visit_never_signed_in_has_no_sign_in()
    {
        await Log().SeenAsync(Mark, Cancel);

        _store.Kept[Mark].LastSignIn.ShouldBeNull();
    }

    [Fact]
    public async Task Forgetting_the_account_removes_what_was_kept_and_the_next_visit_is_written_again()
    {
        var log = Log();
        await log.SeenAsync(Mark, Cancel);

        await log.ForgetAsync(Mark, Cancel);
        await log.SeenAsync(Mark, Cancel);

        _store.Writes.ShouldBe(2);
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}

using DownfallArena.Application.Accounts;
using DownfallArena.Application.Tests.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Accounts;

/// <summary>
/// Suspending an account (ADR 0178): with a reason its player reads, every session ended by the same write, lifted by
/// a restore, and its seats due to other players a day later.
/// </summary>
public sealed class AccountSuspensionTests
{
    private const string Reason = "Spamming invitations to strangers.";
    private static readonly ExternalIdentity Mark = new(ExternalIdentity.Google, "109876543210987654321");

    private readonly MemoryAccountStore _store = new();
    private readonly FixedTimeProvider _clock = new(FixedTimeProvider.Default);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private AccountRegistry Registry() => new(_store, _clock);

    private AccountSuspension Suspension() => AccountSuspension.Create(Reason, "operator", _clock.GetUtcNow()).Value;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too short")]
    public void A_suspension_without_a_reason_its_player_can_read_is_refused(string? reason)
    {
        AccountSuspension.Create(reason, "operator", FixedTimeProvider.Default).Error.ShouldBe(AccountSuspension.Invalid);
    }

    [Fact]
    public void A_reason_longer_than_its_limit_is_refused_and_one_at_it_is_kept_trimmed()
    {
        AccountSuspension.Create(new string('a', AccountSuspension.LongestReason + 1), "operator", FixedTimeProvider.Default).IsFailure.ShouldBeTrue();
        AccountSuspension.Create($"  {new string('a', AccountSuspension.LongestReason)}  ", "operator", FixedTimeProvider.Default).Value.Reason
            .ShouldBe(new string('a', AccountSuspension.LongestReason));
    }

    [Fact]
    public async Task Suspending_an_account_keeps_why_and_ends_every_session_it_holds()
    {
        var registry = Registry();
        var account = (await registry.RegisterAsync(Mark, "MarkF", Cancel)).Value;

        var suspended = (await registry.SuspendAsync(account.Id, Suspension(), Cancel)).Value;

        suspended.Suspension.ShouldNotBeNull().Reason.ShouldBe(Reason);
        suspended.Generation.ShouldBe(account.Generation + 1);
        (await registry.FindAsync(account.Id, Cancel)).ShouldBe(suspended);
    }

    [Fact]
    public async Task An_account_already_suspended_keeps_the_suspension_it_has()
    {
        var registry = Registry();
        var account = (await registry.RegisterAsync(Mark, "MarkF", Cancel)).Value;
        await registry.SuspendAsync(account.Id, Suspension(), Cancel);

        var again = await registry.SuspendAsync(account.Id, AccountSuspension.Create("Another reason, written later.", "ops", _clock.GetUtcNow()).Value, Cancel);

        again.Error.ShouldBe(AccountErrors.AlreadySuspended);
        (await registry.FindAsync(account.Id, Cancel)).ShouldNotBeNull().Suspension.ShouldNotBeNull().Reason.ShouldBe(Reason);
    }

    [Fact]
    public async Task Restoring_an_account_lifts_its_suspension_and_keeps_its_sessions_ended()
    {
        var registry = Registry();
        var account = (await registry.RegisterAsync(Mark, "MarkF", Cancel)).Value;
        await registry.SuspendAsync(account.Id, Suspension(), Cancel);

        var restored = (await registry.RestoreAsync(account.Id, Cancel)).Value;

        restored.Suspension.ShouldBeNull();
        restored.Generation.ShouldBe(account.Generation + 1);
    }

    [Fact]
    public async Task An_account_in_good_standing_cannot_be_restored()
    {
        var registry = Registry();
        var account = (await registry.RegisterAsync(Mark, "MarkF", Cancel)).Value;

        (await registry.RestoreAsync(account.Id, Cancel)).Error.ShouldBe(AccountErrors.NotSuspended);
    }

    [Fact]
    public async Task Nobody_is_suspended_by_an_id_no_account_holds()
    {
        (await Registry().SuspendAsync(AccountId.New(), Suspension(), Cancel)).Error.ShouldBe(AccountErrors.NotFound);
    }

    [Fact]
    public void A_suspended_account_s_seats_fall_due_a_day_later()
    {
        var suspension = Suspension();

        suspension.SeatsDue(suspension.At + AccountSuspension.SeatsKeptFor - TimeSpan.FromMinutes(1)).ShouldBeFalse();
        suspension.SeatsDue(suspension.At + AccountSuspension.SeatsKeptFor).ShouldBeTrue();
    }
}

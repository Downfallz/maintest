using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>What every activity store promises (ADR 0174), played against each adapter.</summary>
public abstract class ActivityStoreContract
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 20, 14, 0, TimeSpan.Zero);

    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<IAccountActivityStore> StoreAsync();

    [Fact]
    public async Task An_account_none_was_kept_for_has_no_activity()
    {
        var store = await StoreAsync();

        (await store.FindAsync(AccountId.New(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task Activity_reads_back_as_it_was_saved_and_a_save_replaces_the_one_before()
    {
        var store = await StoreAsync();
        var account = AccountId.New();

        await store.SaveAsync(new AccountActivity(account, null, Now), Cancel);
        await store.SaveAsync(new AccountActivity(account, Now, Now.AddHours(1)), Cancel);

        (await store.FindAsync(account, Cancel)).ShouldBe(new AccountActivity(account, Now, Now.AddHours(1)));
    }

    [Fact]
    public async Task An_account_s_mute_from_feedback_reads_back_with_its_times_and_goes_once_lifted()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        var muted = new AccountActivity(account, Now, Now) { FeedbackMute = FeedbackMute.Begun(Now, "mark", FeedbackMute.BySpam) };

        await store.SaveAsync(muted, Cancel);
        (await store.FindAsync(account, Cancel)).ShouldBe(muted);

        await store.SaveAsync(muted with { FeedbackMute = null }, Cancel);
        (await store.FindAsync(account, Cancel)).ShouldBe(new AccountActivity(account, Now, Now));
    }

    [Fact]
    public async Task Every_account_kept_is_listed_and_a_forgotten_one_is_not()
    {
        var store = await StoreAsync();
        var kept = AccountId.New();
        var gone = AccountId.New();
        await store.SaveAsync(new AccountActivity(kept, Now, Now), Cancel);
        await store.SaveAsync(new AccountActivity(gone, Now, Now), Cancel);

        await store.ForgetAsync(gone, Cancel);
        await store.ForgetAsync(AccountId.New(), Cancel);

        (await store.ListAsync(Cancel)).ShouldBe([new AccountActivity(kept, Now, Now)]);
        (await store.FindAsync(gone, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_store_with_nothing_kept_lists_nothing()
    {
        var store = await StoreAsync();

        (await store.ListAsync(Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Forgetting_an_account_on_a_store_with_nothing_kept_is_nothing_to_do()
    {
        var store = await StoreAsync();

        await store.ForgetAsync(AccountId.New(), Cancel);

        (await store.ListAsync(Cancel)).ShouldBeEmpty();
    }
}

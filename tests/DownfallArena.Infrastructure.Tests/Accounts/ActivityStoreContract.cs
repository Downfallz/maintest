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
}

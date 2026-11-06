using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>What every preference store promises, played against each adapter.</summary>
public abstract class PreferenceStoreContract
{
    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<IPreferenceStore> StoreAsync();

    [Fact]
    public async Task An_account_that_never_saved_has_no_settings()
    {
        var store = await StoreAsync();

        (await store.GetAsync(AccountId.New(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task Settings_read_back_as_they_were_saved_and_a_save_replaces_the_one_before()
    {
        var store = await StoreAsync();
        var account = AccountId.New();

        await store.SaveAsync(account, new GamePreferences(ReadingPace.Auto, 1.5m, Help: false, ReducedMotion: true), Cancel);
        (await store.GetAsync(account, Cancel)).ShouldBe(new GamePreferences(ReadingPace.Auto, 1.5m, Help: false, ReducedMotion: true));

        await store.SaveAsync(account, GamePreferences.Default, Cancel);
        (await store.GetAsync(account, Cancel)).ShouldBe(GamePreferences.Default);
    }

    [Fact]
    public async Task Forgetting_an_account_leaves_its_settings_gone_and_others_untouched()
    {
        var store = await StoreAsync();
        var gone = AccountId.New();
        var kept = AccountId.New();
        await store.SaveAsync(gone, GamePreferences.Default, Cancel);
        await store.SaveAsync(kept, GamePreferences.Default, Cancel);

        await store.ForgetAsync(gone, Cancel);

        (await store.GetAsync(gone, Cancel)).ShouldBeNull();
        (await store.GetAsync(kept, Cancel)).ShouldBe(GamePreferences.Default);
    }
}

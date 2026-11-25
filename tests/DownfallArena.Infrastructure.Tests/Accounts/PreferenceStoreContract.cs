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

        await store.SaveAsync(account, new GamePreferences(ReadingPace.Auto, 1.5m, Help: false, ReducedMotion: true, Reactions: false), Cancel);
        (await store.GetAsync(account, Cancel)).ShouldBe(new GamePreferences(ReadingPace.Auto, 1.5m, Help: false, ReducedMotion: true, Reactions: false));

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

    [Fact]
    public async Task Forgetting_an_account_where_nothing_was_ever_saved_does_nothing()
    {
        var store = await StoreAsync();
        var account = AccountId.New();

        await store.ForgetAsync(account, Cancel);

        (await store.GetAsync(account, Cancel)).ShouldBeNull();
        (await store.GetTutorialsAsync(account, Cancel)).ShouldBe(TutorialProgress.None);
    }

    [Fact]
    public async Task An_account_that_kept_no_tutorial_has_none_done_or_skipped()
    {
        var store = await StoreAsync();

        (await store.GetTutorialsAsync(AccountId.New(), Cancel)).ShouldBe(TutorialProgress.None);
    }

    [Fact]
    public async Task Tutorials_done_and_skipped_read_back_as_saved_and_leave_the_account_without_settings()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        var kept = TutorialProgress.None.WithDone("sc-repo-tutorial-your-first-combat").WithSkipped("sc-repo-tutorial-who-acts-first");

        await store.SaveTutorialsAsync(account, kept, Cancel);

        (await store.GetTutorialsAsync(account, Cancel)).ShouldBe(kept);
        (await store.GetAsync(account, Cancel)).ShouldBeNull("skipping a tutorial chooses no settings");
    }

    [Fact]
    public async Task Saving_settings_leaves_the_tutorials_as_they_were()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        var kept = TutorialProgress.None.WithSkipped("sc-repo-tutorial-your-first-combat");
        await store.SaveTutorialsAsync(account, kept, Cancel);

        await store.SaveAsync(account, GamePreferences.Default, Cancel);

        (await store.GetTutorialsAsync(account, Cancel)).ShouldBe(kept);
    }

    [Fact]
    public async Task Forgetting_an_account_forgets_its_tutorials_too()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.SaveTutorialsAsync(account, TutorialProgress.None.WithDone("sc-repo-tutorial-your-first-combat"), Cancel);

        await store.ForgetAsync(account, Cancel);

        (await store.GetTutorialsAsync(account, Cancel)).ShouldBe(TutorialProgress.None);
    }

    [Fact]
    public async Task An_account_that_never_chose_lets_every_notice_pop_up()
    {
        var store = await StoreAsync();

        (await store.GetNoticesAsync(AccountId.New(), Cancel)).ShouldBe(NoticePreferences.Default);
    }

    [Fact]
    public async Task What_pops_up_reads_back_as_saved_and_leaves_the_account_without_settings()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        var chosen = new NoticePreferences(Turns: true, Invitations: false, Results: false);

        await store.SaveNoticesAsync(account, chosen, Cancel);

        (await store.GetNoticesAsync(account, Cancel)).ShouldBe(chosen);
        (await store.GetAsync(account, Cancel)).ShouldBeNull("choosing what pops up chooses no game settings");
    }

    [Fact]
    public async Task Forgetting_an_account_forgets_what_pops_up()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.SaveNoticesAsync(account, new NoticePreferences(Turns: false, Invitations: false, Results: false), Cancel);

        await store.ForgetAsync(account, Cancel);

        (await store.GetNoticesAsync(account, Cancel)).ShouldBe(NoticePreferences.Default);
    }

    [Fact]
    public async Task Every_account_with_something_kept_is_listed_once_and_a_forgotten_one_is_not()
    {
        var store = await StoreAsync();
        var (settings, tutorials, both, gone) = (AccountId.New(), AccountId.New(), AccountId.New(), AccountId.New());
        await store.SaveAsync(settings, GamePreferences.Default, Cancel);
        await store.SaveTutorialsAsync(tutorials, TutorialProgress.None.WithDone("sc-repo-tutorial-your-first-combat"), Cancel);
        await store.SaveTutorialsAsync(both, TutorialProgress.None.WithDone("sc-repo-tutorial-your-first-combat"), Cancel);
        await store.SaveNoticesAsync(both, NoticePreferences.Default, Cancel);
        await store.SaveTutorialsAsync(gone, TutorialProgress.None.WithDone("sc-repo-tutorial-your-first-combat"), Cancel);
        await store.ForgetAsync(gone, Cancel);

        var listed = await store.ListAccountsAsync(Cancel);

        listed.ShouldContain(settings);
        listed.ShouldContain(tutorials);
        listed.Count(account => account == both).ShouldBe(1);
        listed.ShouldNotContain(gone);
    }
}

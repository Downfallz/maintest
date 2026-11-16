using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>What every account store promises the registry, played against each adapter.</summary>
public abstract class AccountStoreContract
{
    private static readonly ExternalIdentity Mark = new(ExternalIdentity.Google, "109876543210987654321");
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 20, 14, 0, TimeSpan.Zero);

    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<IAccountStore> StoreAsync();

    private static Account AnAccount(string name = "MarkF") =>
        new(AccountId.New(), Mark, Username.Create(name).Value, Now, Generation: 1);

    [Fact]
    public async Task A_suspension_reads_back_as_it_was_saved_and_a_restore_clears_it()
    {
        var store = await StoreAsync();
        var account = AnAccount();
        var suspension = new AccountSuspension("Spamming invitations to strangers.", "mark", Now);
        await store.SaveAsync(account with { Suspension = suspension }, Cancel);

        var suspended = (await store.FindAsync(account.Id, Cancel)).ShouldNotBeNull();
        await store.SaveAsync(suspended with { Suspension = null }, Cancel);
        var restored = (await store.FindAsync(account.Id, Cancel)).ShouldNotBeNull();

        suspended.Suspension.ShouldBe(suspension);
        restored.Suspension.ShouldBeNull();
    }

    [Fact]
    public async Task An_account_reads_back_as_it_was_saved_and_a_later_save_replaces_it()
    {
        var store = await StoreAsync();
        var account = AnAccount();
        await store.SaveAsync(account, Cancel);
        await store.SaveAsync(account with { Generation = 2 }, Cancel);

        var read = (await store.FindAsync(account.Id, Cancel)).ShouldNotBeNull();

        read.Id.ShouldBe(account.Id);
        read.Identity.ShouldBe(Mark);
        read.Username.Value.ShouldBe("MarkF");
        read.CreatedAt.ShouldBe(Now);
        read.Generation.ShouldBe(2);
    }

    [Fact]
    public async Task Every_account_saved_is_listed_once_and_a_deleted_one_is_not()
    {
        var store = await StoreAsync();
        var mark = AnAccount("MarkF");
        var julie = AnAccount("Julie");
        var gone = AnAccount("Gone");
        foreach (var account in new[] { mark, julie, gone })
        {
            await store.SaveAsync(account, Cancel);
        }

        await store.SaveAsync(mark with { Generation = 3 }, Cancel);
        await store.DeleteAsync(gone.Id, Cancel);
        await store.TryReserveUsernameAsync(new UsernameReservation(mark.Username, mark.Id, Now), Cancel);
        await store.SessionKeyAsync(Cancel);

        var listed = await store.ListAsync(Cancel);

        listed.Select(account => account.Id).ShouldBe([mark.Id, julie.Id], ignoreOrder: true);
        listed.Single(account => account.Id == mark.Id).Generation.ShouldBe(3);
    }

    [Fact]
    public async Task A_store_with_no_account_lists_none()
    {
        var store = await StoreAsync();

        (await store.ListAsync(Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_account_nobody_saved_is_not_found_and_deleting_it_says_so()
    {
        var store = await StoreAsync();

        (await store.FindAsync(AccountId.New(), Cancel)).ShouldBeNull();
        (await store.DeleteAsync(AccountId.New(), Cancel)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_deleted_account_is_gone()
    {
        var store = await StoreAsync();
        var account = AnAccount();
        await store.SaveAsync(account, Cancel);

        (await store.DeleteAsync(account.Id, Cancel)).ShouldBeTrue();

        (await store.FindAsync(account.Id, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_name_is_reserved_once_and_found_whatever_its_case()
    {
        var store = await StoreAsync();
        var first = new UsernameReservation(Username.Create("MarkF").Value, AccountId.New(), Now);
        var second = new UsernameReservation(Username.Create("markf").Value, AccountId.New(), Now);

        (await store.TryReserveUsernameAsync(first, Cancel)).ShouldBeTrue();
        (await store.TryReserveUsernameAsync(second, Cancel)).ShouldBeFalse();

        var held = (await store.FindUsernameAsync(Username.Create("MARKF").Value, Cancel)).ShouldNotBeNull();
        held.Account.ShouldBe(first.Account);
        held.Username.Value.ShouldBe("MarkF");
        held.ReservedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Two_reservations_of_one_name_at_once_leave_one_holder()
    {
        var store = await StoreAsync();
        var name = Username.Create("MarkF").Value;
        var attempts = Enumerable.Range(0, 8).Select(_ => new UsernameReservation(name, AccountId.New(), Now)).ToArray();

        var won = await Task.WhenAll(attempts.Select(attempt => Task.Run(() => store.TryReserveUsernameAsync(attempt, Cancel), Cancel)));

        won.Count(result => result).ShouldBe(1);
        (await store.FindUsernameAsync(name, Cancel)).ShouldNotBeNull().Account.ShouldBe(attempts[Array.IndexOf(won, true)].Account);
    }

    [Fact]
    public async Task Registrations_reclaiming_one_abandoned_name_at_once_leave_one_holder()
    {
        var store = await StoreAsync();
        var name = Username.Create("MarkF").Value;
        var abandoned = new UsernameReservation(name, AccountId.New(), Now);
        await store.TryReserveUsernameAsync(abandoned, Cancel);
        var attempts = Enumerable.Range(0, 16).Select(_ => new UsernameReservation(name, AccountId.New(), Now)).ToArray();

        // What the registry does with an abandoned name: free the holder it read, then reserve. A release that
        // read the abandoned holder must not delete a reservation made after it read.
        var won = await Task.WhenAll(attempts.Select(attempt => Task.Run(
            async () =>
            {
                await store.ReleaseUsernameAsync(abandoned, Cancel);
                return await store.TryReserveUsernameAsync(attempt, Cancel);
            },
            Cancel)));

        won.Count(result => result).ShouldBe(1);
        (await store.FindUsernameAsync(name, Cancel)).ShouldNotBeNull().Account.ShouldBe(attempts[Array.IndexOf(won, true)].Account);
    }

    [Fact]
    public async Task A_name_is_released_only_for_the_account_that_holds_it()
    {
        var store = await StoreAsync();
        var held = new UsernameReservation(Username.Create("MarkF").Value, AccountId.New(), Now);
        await store.TryReserveUsernameAsync(held, Cancel);

        (await store.ReleaseUsernameAsync(held with { Account = AccountId.New() }, Cancel)).ShouldBeFalse();
        (await store.FindUsernameAsync(held.Username, Cancel)).ShouldNotBeNull();

        (await store.ReleaseUsernameAsync(held, Cancel)).ShouldBeTrue();
        (await store.FindUsernameAsync(held.Username, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_sign_in_is_bound_once_and_unbound_only_from_its_account()
    {
        var store = await StoreAsync();
        var account = AccountId.New();

        (await store.TryBindSubjectAsync(Mark, account, Cancel)).ShouldBeTrue();
        (await store.TryBindSubjectAsync(Mark, AccountId.New(), Cancel)).ShouldBeFalse();
        (await store.FindBySubjectAsync(Mark, Cancel)).ShouldBe(account);

        (await store.UnbindSubjectAsync(Mark, AccountId.New(), Cancel)).ShouldBeFalse();
        (await store.UnbindSubjectAsync(Mark, account, Cancel)).ShouldBeTrue();
        (await store.FindBySubjectAsync(Mark, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_subject_with_characters_a_path_would_read_is_kept_apart_from_its_neighbours()
    {
        var store = await StoreAsync();
        var odd = new ExternalIdentity(ExternalIdentity.Google, "../by-name/markf");
        var account = AccountId.New();

        (await store.TryBindSubjectAsync(odd, account, Cancel)).ShouldBeTrue();

        (await store.FindBySubjectAsync(odd, Cancel)).ShouldBe(account);
        (await store.FindUsernameAsync(Username.Create("markf").Value, Cancel)).ShouldBeNull();
        (await store.FindBySubjectAsync(new ExternalIdentity(ExternalIdentity.Development, "../by-name/markf"), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task The_session_key_is_made_once_and_the_same_on_every_later_read()
    {
        var store = await StoreAsync();

        var keys = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => store.SessionKeyAsync(Cancel), Cancel)));

        keys[0].Length.ShouldBe(32);
        keys.ShouldAllBe(key => key.SequenceEqual(keys[0]));
        (await store.SessionKeyAsync(Cancel)).ShouldBe(keys[0]);
    }
}

using DownfallArena.Application.Accounts;
using DownfallArena.Application.Tests.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Tests.Accounts;

/// <summary>
/// The order and the conditions of the registry's writes: what a stop between two of them leaves (#1216, #1227), and
/// what the release beside this one during a deploy, writing the same account, cannot undo (#1217).
/// </summary>
public sealed class AccountWriteOrderTests
{
    private static readonly ExternalIdentity Google = new(ExternalIdentity.Google, "109876543210987654321");
    private static readonly ExternalIdentity Discord = new(ExternalIdentity.Discord, "80351110224678912");
    private static readonly ExternalIdentity Microsoft = new(ExternalIdentity.Microsoft, "00000000-0000-0000-66f3-3332eca7ea81");

    private readonly MemoryAccountStore _store = new();
    private readonly MovableClock _clock = new(FixedTimeProvider.Default);

    private AccountRegistry Registry() => new(_store, _clock);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<Account> RegisteredAsync(ExternalIdentity identity, string name) =>
        (await Registry().RegisterAsync(identity, name, Cancel)).Value;

    private async Task<Account> LinkedAsync()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        return (await Registry().LinkAsync(mark.Id, Discord, cancellationToken: Cancel)).Value;
    }

    // What a deletion on the other release writes: the bindings, the account, the name.
    private void DeletedElsewhere(Account account)
    {
        foreach (var identity in account.Identities.All)
        {
            _store.Subjects.Remove(identity);
        }

        _store.Accounts.Remove(account.Id);
        _store.Names.Remove(account.Username.Key);
    }

    [Fact]
    public async Task A_removal_stopped_before_its_binding_went_leaves_no_binding_once_the_account_is_deleted()
    {
        var mark = await LinkedAsync();
        _store.FailNextUnbinding = true;
        await Should.ThrowAsync<IOException>(() => Registry().UnlinkAsync(mark.Id, Discord, cancellationToken: Cancel));

        (await Registry().DeleteAsync(mark.Id, Cancel)).ShouldBeTrue();

        _store.Subjects.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_removal_stopped_before_its_binding_went_is_finished_by_the_next_way_added()
    {
        var mark = await LinkedAsync();
        _store.FailNextUnbinding = true;
        await Should.ThrowAsync<IOException>(() => Registry().UnlinkAsync(mark.Id, Discord, cancellationToken: Cancel));

        var linked = await Registry().LinkAsync(mark.Id, Microsoft, cancellationToken: Cancel);

        linked.Value.Unbinding.ShouldBeNull();
        _store.Subjects.Keys.ShouldBe([Google, Microsoft], ignoreOrder: true);
    }

    [Fact]
    public async Task A_removal_that_finished_keeps_no_identity_on_the_account()
    {
        var mark = await LinkedAsync();

        var unlinked = await Registry().UnlinkAsync(mark.Id, Discord, cancellationToken: Cancel);

        unlinked.Value.Unbinding.ShouldBeNull();
        _store.Accounts[mark.Id].Unbinding.ShouldBeNull();
        _store.Accounts[mark.Id].Identities.All.ShouldBe([Google]);
    }

    [Fact]
    public async Task A_way_added_to_an_account_the_other_release_deleted_meanwhile_writes_nothing_back()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.BeforeNextAccountWrite = () => DeletedElsewhere(mark);

        var linked = await Registry().LinkAsync(mark.Id, Discord, cancellationToken: Cancel);

        linked.Error.ShouldBe(AccountErrors.NotFound);
        _store.Accounts.ShouldNotContainKey(mark.Id);
        _store.Subjects.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_sign_out_everywhere_the_other_release_raced_raises_the_generation_past_both()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.BeforeNextAccountWrite = () => _store.Accounts[mark.Id] = mark with { Generation = mark.Generation + 1 };

        var raised = await Registry().SignOutEverywhereAsync(mark.Id, Cancel);

        raised.ShouldNotBeNull().Generation.ShouldBe(mark.Generation + 2);
        _store.Accounts[mark.Id].Generation.ShouldBe(mark.Generation + 2);
    }

    [Fact]
    public async Task A_rename_keeps_a_suspension_the_other_release_wrote_meanwhile()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        var suspension = new AccountSuspension("Spamming invitations to strangers.", "operator", _clock.GetUtcNow());
        _store.BeforeNextAccountWrite = () => _store.Accounts[mark.Id] = mark with { Generation = mark.Generation + 1, Suspension = suspension };

        var renamed = await Registry().RenameAsync(mark.Id, "Marcus", Cancel);

        renamed.Value.Username.Value.ShouldBe("Marcus");
        _store.Accounts[mark.Id].Suspension.ShouldBe(suspension);
        _store.Accounts[mark.Id].Generation.ShouldBe(mark.Generation + 1);
    }

    [Fact]
    public async Task A_suspension_of_an_account_the_other_release_deleted_meanwhile_writes_nothing_back()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.BeforeNextAccountWrite = () => DeletedElsewhere(mark);

        var suspended = await Registry().SuspendAsync(mark.Id, new AccountSuspension("Spam.", "operator", _clock.GetUtcNow()), Cancel);

        suspended.Error.ShouldBe(AccountErrors.NotFound);
        _store.Accounts.ShouldNotContainKey(mark.Id);
    }

    [Fact]
    public async Task A_deletion_frees_a_way_the_other_release_added_meanwhile()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.BeforeNextAccountWrite = () =>
        {
            _store.Subjects[Discord] = mark.Id;
            _store.Accounts[mark.Id] = mark with { Identities = mark.Identities.With(Discord) };
        };

        (await Registry().DeleteAsync(mark.Id, Cancel)).ShouldBeTrue();

        _store.Accounts.ShouldBeEmpty();
        _store.Subjects.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_way_added_whose_retry_finds_the_session_ended_leaves_no_binding()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.BeforeNextAccountWrite = () => _store.Accounts[mark.Id] = mark with { Generation = mark.Generation + 1 };

        var linked = await Registry().LinkAsync(mark.Id, Discord, mark.Generation, Cancel);

        linked.Error.ShouldBe(AccountErrors.SessionEnded);
        _store.Subjects.Keys.ShouldBe([Google]);
    }

    [Fact]
    public async Task A_way_added_whose_retry_finds_another_of_its_provider_leaves_no_binding()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        var otherDiscord = new ExternalIdentity(ExternalIdentity.Discord, "11111111111111111");
        _store.BeforeNextAccountWrite = () =>
        {
            _store.Subjects[otherDiscord] = mark.Id;
            _store.Accounts[mark.Id] = mark with { Identities = mark.Identities.With(otherDiscord) };
        };

        var linked = await Registry().LinkAsync(mark.Id, Discord, cancellationToken: Cancel);

        linked.Error.ShouldBe(AccountErrors.ProviderLinked);
        _store.Subjects.Keys.ShouldBe([Google, otherDiscord], ignoreOrder: true);
    }

    [Fact]
    public async Task An_upgrade_whose_retry_finds_the_account_suspended_leaves_no_binding_and_no_name()
    {
        var guest = (await Registry().MakeGuestAsync(Cancel)).Value;
        var suspension = new AccountSuspension("Spam.", "operator", _clock.GetUtcNow());
        _store.BeforeNextAccountWrite = () => _store.Accounts[guest.Id] = guest with { Suspension = suspension };

        var upgraded = await Registry().UpgradeAsync(guest.Id, Google, "MarkF", Cancel);

        upgraded.Error.ShouldBe(AccountErrors.Suspended(suspension));
        _store.Subjects.ShouldNotContainKey(Google);
        _store.Names.ShouldNotContainKey("markf");
    }

    [Fact]
    public async Task A_guest_deletion_that_finds_the_guest_upgraded_meanwhile_removes_nothing()
    {
        var guest = (await Registry().MakeGuestAsync(Cancel)).Value;
        var upgraded = guest with { Identities = AccountIdentities.Of(Google), Username = Username.Create("MarkF").Value };
        _store.BeforeNextAccountWrite = () =>
        {
            _store.Subjects[Google] = guest.Id;
            _store.Accounts[guest.Id] = upgraded;
        };

        var deleted = await Registry().DeleteAsync(guest.Id, account => account.IsGuest, Cancel);

        deleted.ShouldBeFalse();
        _store.Accounts[guest.Id].ShouldBe(upgraded);
        _store.Subjects[Google].ShouldBe(guest.Id);
    }

    [Fact]
    public async Task A_name_held_over_an_hour_and_taken_up_again_by_its_account_is_held_afresh()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.FailNextAccountWrite = true;
        await Should.ThrowAsync<IOException>(() => Registry().RenameAsync(mark.Id, "Marcus", Cancel));
        _clock.Advance(AccountRegistry.AbandonedAfter * 2);
        Result<Account>? registered = null;
        _store.BeforeNextAccountWrite = () => registered = Registry().RegisterAsync(Discord, "Marcus", Cancel).GetAwaiter().GetResult();

        var renamed = await Registry().RenameAsync(mark.Id, "Marcus", Cancel);

        renamed.Value.Username.Value.ShouldBe("Marcus");
        registered.ShouldNotBeNull().Error.ShouldBe(AccountErrors.UsernameTaken);
        _store.Names["marcus"].Account.ShouldBe(mark.Id);
    }

    [Fact]
    public async Task A_name_judged_abandoned_and_held_afresh_by_its_account_meanwhile_is_not_freed()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.FailNextAccountWrite = true;
        await Should.ThrowAsync<IOException>(() => Registry().RenameAsync(mark.Id, "Marcus", Cancel));
        _clock.Advance(AccountRegistry.AbandonedAfter * 2);
        var fresh = new UsernameReservation(Username.Create("Marcus").Value, mark.Id, _clock.GetUtcNow());
        _store.BeforeNextNameRelease = () => _store.Names["marcus"] = fresh;

        var registered = await Registry().RegisterAsync(Discord, "Marcus", Cancel);

        registered.Error.ShouldBe(AccountErrors.UsernameTaken);
        _store.Names["marcus"].ShouldBe(fresh);
    }

    [Fact]
    public async Task An_upgrade_the_other_release_finished_first_keeps_the_name_and_the_binding_it_wrote()
    {
        var guest = (await Registry().MakeGuestAsync(Cancel)).Value;
        var upgraded = guest with
        {
            Identities = AccountIdentities.Of(Google),
            Username = Username.Create("MarkF").Value,
            Generation = guest.Generation + 1,
        };
        _store.BeforeNextAccountWrite = () => _store.Accounts[guest.Id] = upgraded;

        var again = await Registry().UpgradeAsync(guest.Id, Google, "MarkF", Cancel);

        again.Error.ShouldBe(AccountErrors.NotAGuest);
        _store.Subjects[Google].ShouldBe(guest.Id);
        _store.Names["markf"].Account.ShouldBe(guest.Id);
    }

    [Fact]
    public async Task A_refused_way_whose_binding_another_link_adopted_since_the_refusal_leaves_it()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.BeforeNextAccountWrite = () => _store.Accounts[mark.Id] = mark with { Generation = mark.Generation + 1 };
        _store.AccountReadsBeforeHook = 2;
        _store.BeforeAccountRead = () => _store.Accounts[mark.Id] = mark with { Generation = mark.Generation + 1, Identities = mark.Identities.With(Discord) };

        var linked = await Registry().LinkAsync(mark.Id, Discord, mark.Generation, Cancel);

        linked.Error.ShouldBe(AccountErrors.SessionEnded);
        _store.Subjects[Discord].ShouldBe(mark.Id);
    }

    [Fact]
    public async Task A_refused_way_whose_binding_another_link_adopts_after_the_cleanup_read_the_account_leaves_it()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.BeforeNextAccountWrite = () => _store.Accounts[mark.Id] = mark with { Generation = mark.Generation + 1 };
        _store.BeforeNextUnbinding = () =>
        {
            _store.AdoptedElsewhere(Discord);
            _store.Accounts[mark.Id] = mark with { Generation = mark.Generation + 2, Identities = mark.Identities.With(Discord) };
        };

        var linked = await Registry().LinkAsync(mark.Id, Discord, mark.Generation, Cancel);

        linked.Error.ShouldBe(AccountErrors.SessionEnded);
        _store.Subjects[Discord].ShouldBe(mark.Id);
        (await Registry().FindAsync(Discord, Cancel))!.Id.ShouldBe(mark.Id);
    }

    [Fact]
    public async Task A_refused_upgrade_whose_binding_another_upgrade_adopts_after_the_cleanup_read_the_account_leaves_it()
    {
        var guest = (await Registry().MakeGuestAsync(Cancel)).Value;
        var suspension = new AccountSuspension("Spam.", "operator", _clock.GetUtcNow());
        _store.BeforeNextAccountWrite = () => _store.Accounts[guest.Id] = guest with { Suspension = suspension };
        _store.BeforeNextUnbinding = () =>
        {
            _store.AdoptedElsewhere(Google);
            _store.Accounts[guest.Id] = guest with { Identities = AccountIdentities.Of(Google), Generation = guest.Generation + 1 };
        };

        var upgraded = await Registry().UpgradeAsync(guest.Id, Google, "MarkF", Cancel);

        upgraded.IsFailure.ShouldBeTrue();
        _store.Subjects[Google].ShouldBe(guest.Id);
    }

    [Fact]
    public async Task A_way_added_while_every_write_of_the_account_loses_to_another_process_throws_and_leaves_a_binding_that_reaches_nothing()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.BeforeEveryAccountWrite = () => _store.Accounts[mark.Id] = _store.Accounts[mark.Id] with { };

        await Should.ThrowAsync<IOException>(() => Registry().LinkAsync(mark.Id, Discord, cancellationToken: Cancel));

        _store.RefusedWrites.ShouldBe(5);
        _store.BeforeEveryAccountWrite = null;
        _store.Subjects[Discord].ShouldBe(mark.Id);
        _store.Accounts[mark.Id].Identities.Contains(Discord).ShouldBeFalse();
        (await Registry().FindAsync(Discord, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task An_upgrade_while_every_write_of_the_account_loses_to_another_process_throws_and_leaves_its_binding_and_name_held()
    {
        var guest = (await Registry().MakeGuestAsync(Cancel)).Value;
        _store.BeforeEveryAccountWrite = () => _store.Accounts[guest.Id] = _store.Accounts[guest.Id] with { };

        await Should.ThrowAsync<IOException>(() => Registry().UpgradeAsync(guest.Id, Google, "MarkF", Cancel));

        _store.RefusedWrites.ShouldBe(5);
        _store.BeforeEveryAccountWrite = null;
        _store.Subjects[Google].ShouldBe(guest.Id);
        _store.Names["markf"].Account.ShouldBe(guest.Id);
        _store.Accounts[guest.Id].IsGuest.ShouldBeTrue();
        (await Registry().FindAsync(Google, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_deletion_while_every_removal_of_the_account_loses_to_another_process_throws_and_keeps_the_account()
    {
        var mark = await LinkedAsync();
        _store.BeforeEveryAccountWrite = () => _store.Accounts[mark.Id] = _store.Accounts[mark.Id] with { };

        await Should.ThrowAsync<IOException>(() => Registry().DeleteAsync(mark.Id, Cancel));

        _store.RefusedWrites.ShouldBe(5);
        _store.Accounts.ShouldContainKey(mark.Id);
        _store.Names["markf"].Account.ShouldBe(mark.Id);
    }

    [Fact]
    public async Task A_way_added_that_found_its_binding_made_by_another_link_leaves_it_when_refused()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.Subjects[Discord] = mark.Id;
        _store.BeforeNextAccountWrite = () => _store.Accounts[mark.Id] = mark with { Generation = mark.Generation + 1 };

        var linked = await Registry().LinkAsync(mark.Id, Discord, mark.Generation, Cancel);

        linked.Error.ShouldBe(AccountErrors.SessionEnded);
        _store.Subjects[Discord].ShouldBe(mark.Id);
    }

    [Fact]
    public async Task An_abandoned_registration_resumed_by_another_release_before_its_removal_is_kept()
    {
        _store.FailNextBinding = true;
        await Should.ThrowAsync<IOException>(() => Registry().RegisterAsync(Google, "MarkF", Cancel));
        var stopped = _store.Accounts.Values.Single();
        _clock.Advance(AccountRegistry.AbandonedAfter);
        _store.BeforeNextAccountWrite = () =>
        {
            _store.Subjects[Google] = stopped.Id;
            _store.Accounts[stopped.Id] = stopped with { };
        };

        await Registry().RegisterAsync(Discord, "MarkF", Cancel);

        _store.Accounts.ShouldContainKey(stopped.Id);
    }

    [Fact]
    public async Task An_upgrade_stopped_before_its_account_was_written_then_asked_with_another_name_frees_the_first_an_hour_on()
    {
        var guest = (await Registry().MakeGuestAsync(Cancel)).Value;
        _store.FailNextAccountWrite = true;
        await Should.ThrowAsync<IOException>(() => Registry().UpgradeAsync(guest.Id, Google, "MarkF", Cancel));
        var upgraded = await Registry().UpgradeAsync(guest.Id, Google, "Marcus", Cancel);
        _clock.Advance(AccountRegistry.AbandonedAfter);

        var registered = await Registry().RegisterAsync(Discord, "MarkF", Cancel);

        upgraded.Value.Username.Value.ShouldBe("Marcus");
        registered.Value.Username.Value.ShouldBe("MarkF");
        _store.Accounts[guest.Id].Username.Value.ShouldBe("Marcus");
        _store.Subjects[Google].ShouldBe(guest.Id);
    }

    [Fact]
    public async Task A_name_an_upgrade_held_and_left_stays_held_within_the_hour()
    {
        var guest = (await Registry().MakeGuestAsync(Cancel)).Value;
        _store.FailNextAccountWrite = true;
        await Should.ThrowAsync<IOException>(() => Registry().UpgradeAsync(guest.Id, Google, "MarkF", Cancel));
        await Registry().UpgradeAsync(guest.Id, Google, "Marcus", Cancel);
        _clock.Advance(AccountRegistry.AbandonedAfter / 2);

        (await Registry().CheckUsernameAsync("MarkF", Cancel)).Error.ShouldBe(AccountErrors.UsernameTaken);
    }

    [Fact]
    public async Task A_rename_stopped_before_its_account_was_written_frees_the_new_name_an_hour_on_and_keeps_the_account()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.FailNextAccountWrite = true;
        await Should.ThrowAsync<IOException>(() => Registry().RenameAsync(mark.Id, "Marcus", Cancel));
        _clock.Advance(AccountRegistry.AbandonedAfter);

        var registered = await Registry().RegisterAsync(Discord, "Marcus", Cancel);

        registered.IsSuccess.ShouldBeTrue();
        _store.Accounts[mark.Id].Username.Value.ShouldBe("MarkF");
        (await Registry().FindAsync(Google, Cancel)).ShouldNotBeNull().Id.ShouldBe(mark.Id);
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}

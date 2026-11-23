using DownfallArena.Application.Accounts;
using DownfallArena.Application.Tests.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Accounts;

/// <summary>One account, several ways in (ADR 0204): a signed-in player adds or removes an identity.</summary>
public sealed class AccountLinkTests
{
    private static readonly ExternalIdentity Google = new(ExternalIdentity.Google, "109876543210987654321");
    private static readonly ExternalIdentity Discord = new("discord", "80351110224678912");
    private static readonly ExternalIdentity Bastien = new(ExternalIdentity.Google, "201234567890123456789");

    private readonly MemoryAccountStore _store = new();
    private readonly MovableClock _clock = new(FixedTimeProvider.Default);

    private AccountRegistry Registry() => new(_store, _clock);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<Account> RegisteredAsync(ExternalIdentity identity, string name) =>
        (await Registry().RegisterAsync(identity, name, Cancel)).Value;

    [Fact]
    public async Task A_linked_identity_signs_in_to_the_same_account()
    {
        var mark = await RegisteredAsync(Google, "MarkF");

        var linked = await Registry().LinkAsync(mark.Id, Discord, Cancel);

        linked.Value.Identities.All.ShouldBe([Google, Discord]);
        _store.Subjects[Discord].ShouldBe(mark.Id);
        (await Registry().FindAsync(Discord, Cancel)).ShouldNotBeNull().Id.ShouldBe(mark.Id);
        (await Registry().FindAsync(Google, Cancel)).ShouldNotBeNull().Identities.ShouldBe(linked.Value.Identities);
    }

    [Fact]
    public async Task Linking_keeps_the_sessions_the_account_has()
    {
        var mark = await RegisteredAsync(Google, "MarkF");

        (await Registry().LinkAsync(mark.Id, Discord, Cancel)).Value.Generation.ShouldBe(mark.Generation);
    }

    [Fact]
    public async Task Linking_an_identity_the_account_already_holds_changes_nothing()
    {
        var mark = await RegisteredAsync(Google, "MarkF");

        var again = await Registry().LinkAsync(mark.Id, Google, Cancel);

        again.Value.ShouldBe(mark);
    }

    [Fact]
    public async Task An_identity_another_account_holds_is_refused_and_changes_nothing()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        var bastien = await RegisteredAsync(Discord, "Bastien");

        var taken = await Registry().LinkAsync(mark.Id, Discord, Cancel);

        taken.Error.ShouldBe(AccountErrors.IdentityTaken);
        _store.Accounts[mark.Id].ShouldBe(mark);
        _store.Subjects[Discord].ShouldBe(bastien.Id);
    }

    [Fact]
    public async Task A_second_identity_from_a_provider_the_account_already_holds_is_refused_and_binds_nothing()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        var otherGoogle = new ExternalIdentity(ExternalIdentity.Google, "305555555555555555555");

        var refused = await Registry().LinkAsync(mark.Id, otherGoogle, Cancel);

        refused.Error.ShouldBe(AccountErrors.ProviderLinked);
        _store.Accounts[mark.Id].ShouldBe(mark);
        _store.Subjects.ShouldNotContainKey(otherGoogle);
    }

    [Fact]
    public async Task An_identity_registered_by_somebody_else_while_it_was_being_linked_is_refused()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        var stranger = new Account(AccountId.New(), AccountIdentities.Of(Discord), Username.Create("Stranger").Value, FixedTimeProvider.Default, 1);
        _store.BeforeNextBinding = () =>
        {
            _store.Accounts[stranger.Id] = stranger;
            _store.Subjects[Discord] = stranger.Id;
        };

        var lost = await Registry().LinkAsync(mark.Id, Discord, Cancel);

        lost.Error.ShouldBe(AccountErrors.IdentityTaken);
        _store.Accounts[mark.Id].Identities.All.ShouldBe([Google]);
        _store.Subjects[Discord].ShouldBe(stranger.Id);
    }

    [Fact]
    public async Task Two_accounts_linking_one_identity_at_once_leave_it_with_one()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        var bastien = await RegisteredAsync(Bastien, "Bastien");
        _store.SlowReads = true;

        var results = await Task.WhenAll(
            Task.Run(() => Registry().LinkAsync(mark.Id, Discord, Cancel), Cancel),
            Task.Run(() => Registry().LinkAsync(bastien.Id, Discord, Cancel), Cancel));

        results.Count(result => result.IsSuccess).ShouldBe(1);
        results.Single(result => result.IsFailure).Error.ShouldBe(AccountErrors.IdentityTaken);
        var holder = _store.Subjects[Discord];
        _store.Accounts.Values.Where(account => account.Identities.Contains(Discord)).Select(account => account.Id).ShouldBe([holder]);
    }

    [Fact]
    public async Task An_identity_bound_to_an_account_that_no_longer_exists_can_be_linked()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.Subjects[Discord] = AccountId.New();

        var linked = await Registry().LinkAsync(mark.Id, Discord, Cancel);

        linked.Value.Identities.Contains(Discord).ShouldBeTrue();
        _store.Subjects[Discord].ShouldBe(mark.Id);
    }

    [Fact]
    public async Task A_link_interrupted_after_its_binding_signs_in_to_nothing_until_it_is_finished()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.Subjects[Discord] = mark.Id;

        var before = await Registry().FindAsync(Discord, Cancel);
        var linked = await Registry().LinkAsync(mark.Id, Discord, Cancel);

        before.ShouldBeNull();
        linked.Value.Identities.All.ShouldBe([Google, Discord]);
        (await Registry().FindAsync(Discord, Cancel)).ShouldNotBeNull().Id.ShouldBe(mark.Id);
    }

    [Fact]
    public async Task A_binding_the_account_does_not_list_can_register_a_new_account()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        _store.Subjects[Discord] = mark.Id;

        var registered = await Registry().RegisterAsync(Discord, "Bastien", Cancel);

        registered.Value.Id.ShouldNotBe(mark.Id);
        _store.Subjects[Discord].ShouldBe(registered.Value.Id);
    }

    [Fact]
    public async Task A_suspended_account_links_nothing()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        var suspension = new AccountSuspension("Spamming invitations to strangers.", "op", FixedTimeProvider.Default);
        await Registry().SuspendAsync(mark.Id, suspension, Cancel);

        var refused = await Registry().LinkAsync(mark.Id, Discord, Cancel);

        refused.Error.Code.ShouldBe("Account.Suspended");
        _store.Subjects.ShouldNotContainKey(Discord);
    }

    [Fact]
    public async Task Linking_to_an_account_that_is_gone_is_refused_and_binds_nothing()
    {
        (await Registry().LinkAsync(AccountId.New(), Discord, Cancel)).Error.ShouldBe(AccountErrors.NotFound);

        _store.Subjects.ShouldNotContainKey(Discord);
    }

    [Fact]
    public async Task An_unlinked_identity_no_longer_signs_in_and_can_join_another_account()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        await Registry().LinkAsync(mark.Id, Discord, Cancel);

        var unlinked = await Registry().UnlinkAsync(mark.Id, Discord, Cancel);

        unlinked.Value.Identities.All.ShouldBe([Google]);
        _store.Subjects.ShouldNotContainKey(Discord);
        (await Registry().FindAsync(Discord, Cancel)).ShouldBeNull();
        (await Registry().FindAsync(Google, Cancel)).ShouldNotBeNull().Id.ShouldBe(mark.Id);
        var bastien = await RegisteredAsync(Bastien, "Bastien");
        (await Registry().LinkAsync(bastien.Id, Discord, Cancel)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task The_identity_an_account_registered_with_can_go_once_another_is_linked()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        await Registry().LinkAsync(mark.Id, Discord, Cancel);

        (await Registry().UnlinkAsync(mark.Id, Google, Cancel)).Value.Identities.All.ShouldBe([Discord]);

        (await Registry().FindAsync(Discord, Cancel)).ShouldNotBeNull().Id.ShouldBe(mark.Id);
        (await Registry().FindAsync(Google, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task The_last_identity_cannot_be_removed()
    {
        var mark = await RegisteredAsync(Google, "MarkF");

        (await Registry().UnlinkAsync(mark.Id, Google, Cancel)).Error.ShouldBe(AccountErrors.LastIdentity);

        _store.Subjects[Google].ShouldBe(mark.Id);
        _store.Accounts[mark.Id].ShouldBe(mark);
    }

    [Fact]
    public async Task Removing_an_identity_the_account_does_not_hold_is_refused_and_leaves_its_holder_alone()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        await Registry().LinkAsync(mark.Id, Discord, Cancel);
        var bastien = await RegisteredAsync(Bastien, "Bastien");

        (await Registry().UnlinkAsync(mark.Id, Bastien, Cancel)).Error.ShouldBe(AccountErrors.IdentityNotLinked);

        _store.Subjects[Bastien].ShouldBe(bastien.Id);
    }

    [Fact]
    public async Task Removing_an_identity_signs_out_every_session_of_the_account()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        var linked = (await Registry().LinkAsync(mark.Id, Discord, Cancel)).Value;

        var unlinked = await Registry().UnlinkAsync(mark.Id, Discord, Cancel);

        unlinked.Value.Generation.ShouldBe(linked.Generation + 1);
        _store.Accounts[mark.Id].Generation.ShouldBe(linked.Generation + 1);
    }

    [Fact]
    public async Task An_unlink_interrupted_before_its_binding_went_signs_in_to_nothing_and_is_finished_by_unlinking_again()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        await Registry().LinkAsync(mark.Id, Discord, Cancel);
        _store.FailNextUnbinding = true;

        await Should.ThrowAsync<IOException>(() => Registry().UnlinkAsync(mark.Id, Discord, Cancel));
        var stopped = _store.Accounts[mark.Id];
        var reached = await Registry().FindAsync(Discord, Cancel);
        var again = await Registry().UnlinkAsync(mark.Id, Discord, Cancel);

        stopped.Identities.All.ShouldBe([Google]);
        reached.ShouldBeNull();
        again.Value.Identities.All.ShouldBe([Google]);
        _store.Subjects.ShouldNotContainKey(Discord);
    }

    [Fact]
    public async Task A_deleted_account_frees_every_identity_it_held()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        await Registry().LinkAsync(mark.Id, Discord, Cancel);

        (await Registry().DeleteAsync(mark.Id, Cancel)).ShouldBeTrue();

        _store.Subjects.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_name_held_by_an_account_some_identity_of_which_is_bound_is_never_abandoned()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        await Registry().LinkAsync(mark.Id, Discord, Cancel);
        await Registry().UnlinkAsync(mark.Id, Google, Cancel);
        _clock.Advance(AccountRegistry.AbandonedAfter * 2);

        (await Registry().CheckUsernameAsync("MarkF", Cancel)).Error.ShouldBe(AccountErrors.UsernameTaken);
    }

    [Fact]
    public async Task A_linked_identity_cannot_register_a_second_account()
    {
        var mark = await RegisteredAsync(Google, "MarkF");
        await Registry().LinkAsync(mark.Id, Discord, Cancel);

        (await Registry().RegisterAsync(Discord, "MarkTwo", Cancel)).Error.ShouldBe(AccountErrors.AlreadyRegistered);
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}

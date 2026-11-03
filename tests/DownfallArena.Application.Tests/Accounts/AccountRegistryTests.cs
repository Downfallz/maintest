using DownfallArena.Application.Accounts;
using DownfallArena.Application.Tests.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Accounts;

public sealed class AccountRegistryTests
{
    private static readonly ExternalIdentity Mark = new(ExternalIdentity.Google, "109876543210987654321");
    private static readonly ExternalIdentity Bastien = new(ExternalIdentity.Google, "201234567890123456789");

    private readonly MemoryAccountStore _store = new();
    private readonly MovableClock _clock = new(FixedTimeProvider.Default);

    private AccountRegistry Registry() => new(_store, _clock);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_sign_in_that_never_registered_has_no_account()
    {
        (await Registry().FindAsync(Mark, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_registered_sign_in_finds_its_account_with_the_name_it_chose()
    {
        var registry = Registry();

        var registered = (await registry.RegisterAsync(Mark, "MarkF", Cancel)).Value;

        var found = (await registry.FindAsync(Mark, Cancel)).ShouldNotBeNull();
        found.ShouldBe(registered);
        found.Username.Value.ShouldBe("MarkF");
        found.Identity.ShouldBe(Mark);
        found.CreatedAt.ShouldBe(FixedTimeProvider.Default);
        found.Generation.ShouldBe(1);
    }

    [Fact]
    public async Task A_name_another_player_holds_is_taken_whatever_its_case()
    {
        var registry = Registry();
        await registry.RegisterAsync(Mark, "MarkF", Cancel);

        var second = await registry.RegisterAsync(Bastien, "markf", Cancel);

        second.Error.ShouldBe(AccountErrors.UsernameTaken);
        (await registry.FindAsync(Bastien, Cancel)).ShouldBeNull();
        _store.Accounts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_sign_in_registers_once()
    {
        var registry = Registry();
        await registry.RegisterAsync(Mark, "MarkF", Cancel);

        var again = await registry.RegisterAsync(Mark, "MarkTwo", Cancel);

        again.Error.ShouldBe(AccountErrors.AlreadyRegistered);
        (await registry.CheckUsernameAsync("MarkTwo", Cancel)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_name_outside_the_rules_is_refused_before_anything_is_written()
    {
        (await Registry().RegisterAsync(Mark, "Greedy", Cancel)).Error.ShouldBe(AccountErrors.UsernameReserved);

        _store.Names.ShouldBeEmpty();
        _store.Accounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Checking_a_name_says_whether_it_could_be_registered_now()
    {
        var registry = Registry();
        await registry.RegisterAsync(Mark, "MarkF", Cancel);

        (await registry.CheckUsernameAsync("bastien", Cancel)).IsSuccess.ShouldBeTrue();
        (await registry.CheckUsernameAsync("MARKF", Cancel)).Error.ShouldBe(AccountErrors.UsernameTaken);
        (await registry.CheckUsernameAsync("x", Cancel)).Error.ShouldBe(AccountErrors.UsernameInvalid);
    }

    [Fact]
    public async Task A_name_held_for_an_account_that_was_never_written_stays_held_for_an_hour()
    {
        var orphan = new UsernameReservation(Username.Create("MarkF").Value, AccountId.New(), _clock.GetUtcNow());
        _store.Names[orphan.Username.Key] = orphan;
        _clock.Advance(AccountRegistry.AbandonedAfter - TimeSpan.FromMinutes(1));

        (await Registry().RegisterAsync(Bastien, "MarkF", Cancel)).Error.ShouldBe(AccountErrors.UsernameTaken);
    }

    [Fact]
    public async Task A_name_held_for_an_account_that_was_never_written_is_taken_back_after_an_hour()
    {
        var orphan = new UsernameReservation(Username.Create("MarkF").Value, AccountId.New(), _clock.GetUtcNow());
        _store.Names[orphan.Username.Key] = orphan;
        _clock.Advance(AccountRegistry.AbandonedAfter);
        var registry = Registry();

        (await registry.CheckUsernameAsync("MarkF", Cancel)).IsSuccess.ShouldBeTrue();
        var account = (await registry.RegisterAsync(Bastien, "MarkF", Cancel)).Value;

        _store.Names["markf"].Account.ShouldBe(account.Id);
    }

    [Fact]
    public async Task A_name_held_by_a_real_account_is_never_taken_back_however_old()
    {
        var registry = Registry();
        await registry.RegisterAsync(Mark, "MarkF", Cancel);
        _clock.Advance(TimeSpan.FromDays(400));

        (await registry.RegisterAsync(Bastien, "MarkF", Cancel)).Error.ShouldBe(AccountErrors.UsernameTaken);
    }

    [Fact]
    public async Task A_registration_that_loses_the_race_for_its_sign_in_leaves_no_account_and_frees_its_name()
    {
        _store.RefuseNextBinding = true;

        var lost = await Registry().RegisterAsync(Mark, "MarkF", Cancel);

        lost.Error.ShouldBe(AccountErrors.AlreadyRegistered);
        _store.Accounts.ShouldBeEmpty();
        _store.Names.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_sign_in_bound_to_an_account_that_no_longer_exists_can_register_again()
    {
        _store.Subjects[Mark] = AccountId.New();

        var account = (await Registry().RegisterAsync(Mark, "MarkF", Cancel)).Value;

        _store.Subjects[Mark].ShouldBe(account.Id);
    }

    [Fact]
    public async Task A_registration_interrupted_before_its_sign_in_was_bound_is_taken_up_by_the_same_sign_in()
    {
        var registry = Registry();
        _store.FailNextBinding = true;
        await Should.ThrowAsync<IOException>(() => registry.RegisterAsync(Mark, "MarkF", Cancel));
        var stranded = _store.Accounts.Values.Single();

        var account = (await registry.RegisterAsync(Mark, "MarkF", Cancel)).Value;

        account.Id.ShouldBe(stranded.Id);
        (await registry.FindAsync(Mark, Cancel)).ShouldBe(stranded);
        _store.Accounts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_name_held_by_an_account_whose_sign_in_was_never_bound_stays_held_for_an_hour()
    {
        _store.FailNextBinding = true;
        await Should.ThrowAsync<IOException>(() => Registry().RegisterAsync(Mark, "MarkF", Cancel));
        _clock.Advance(AccountRegistry.AbandonedAfter - TimeSpan.FromMinutes(1));

        (await Registry().RegisterAsync(Bastien, "MarkF", Cancel)).Error.ShouldBe(AccountErrors.UsernameTaken);
    }

    [Fact]
    public async Task A_name_held_by_an_account_whose_sign_in_was_never_bound_is_taken_back_after_an_hour()
    {
        _store.FailNextBinding = true;
        await Should.ThrowAsync<IOException>(() => Registry().RegisterAsync(Mark, "MarkF", Cancel));
        var stranded = _store.Accounts.Values.Single();
        _clock.Advance(AccountRegistry.AbandonedAfter);

        var account = (await Registry().RegisterAsync(Bastien, "MarkF", Cancel)).Value;

        _store.Names["markf"].Account.ShouldBe(account.Id);
        _store.Accounts.Keys.ShouldBe([account.Id]);
        _store.Accounts.ContainsKey(stranded.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task Signing_out_everywhere_raises_the_generation_sessions_are_checked_against()
    {
        var registry = Registry();
        var account = (await registry.RegisterAsync(Mark, "MarkF", Cancel)).Value;

        var raised = (await registry.SignOutEverywhereAsync(account.Id, Cancel)).ShouldNotBeNull();

        raised.Generation.ShouldBe(2);
        (await registry.FindAsync(account.Id, Cancel)).ShouldBe(raised);
        (await registry.SignOutEverywhereAsync(AccountId.New(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task Signing_out_everywhere_at_once_raises_the_generation_once_for_each_request()
    {
        var registry = Registry();
        var account = (await registry.RegisterAsync(Mark, "MarkF", Cancel)).Value;
        _store.SlowReads = true;

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => registry.SignOutEverywhereAsync(account.Id, Cancel), Cancel)));

        (await registry.FindAsync(account.Id, Cancel)).ShouldNotBeNull().Generation.ShouldBe(21);
    }

    [Fact]
    public async Task A_deleted_account_frees_its_name_and_its_sign_in()
    {
        var registry = Registry();
        var account = (await registry.RegisterAsync(Mark, "MarkF", Cancel)).Value;

        (await registry.DeleteAsync(account.Id, Cancel)).ShouldBeTrue();

        (await registry.FindAsync(account.Id, Cancel)).ShouldBeNull();
        (await registry.FindAsync(Mark, Cancel)).ShouldBeNull();
        (await registry.CheckUsernameAsync("MarkF", Cancel)).IsSuccess.ShouldBeTrue();
        (await registry.RegisterAsync(Mark, "MarkAgain", Cancel)).Value.Id.ShouldNotBe(account.Id);
        (await registry.DeleteAsync(account.Id, Cancel)).ShouldBeFalse();
    }

    [Fact]
    public async Task Signing_out_everywhere_while_the_account_is_deleted_does_not_bring_it_back()
    {
        var registry = Registry();
        var account = (await registry.RegisterAsync(Mark, "MarkF", Cancel)).Value;
        _store.SlowReads = true;
        _store.SlowWrites = true;

        var raising = Enumerable.Range(0, 10).Select(_ => Task.Run(() => registry.SignOutEverywhereAsync(account.Id, Cancel), Cancel)).ToList();
        var deleting = Task.Run(() => registry.DeleteAsync(account.Id, Cancel), Cancel);
        await Task.WhenAll([.. raising, deleting]);

        (await deleting).ShouldBeTrue();
        _store.Accounts.ShouldNotContainKey(account.Id);
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}

using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>The hourly job's removal of the guests no browser can reach any more (ADR 0207).</summary>
public sealed class IdleGuestsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"idle-guests-{Guid.CreateVersion7():N}");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 24, 20, 0, 0, TimeSpan.Zero));
    private readonly FileAccountStore _accounts;
    private readonly FileActivityStore _activity;
    private readonly FilePreferenceStore _preferences;

    public IdleGuestsTests()
    {
        _accounts = new FileAccountStore(_root);
        _activity = new FileActivityStore(_root);
        _preferences = new FilePreferenceStore(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private IdleGuests Guests() => new(_accounts, _activity, _preferences, _clock);

    private async Task<Account> GuestAsync() => (await new AccountRegistry(_accounts, _clock).MakeGuestAsync(Cancel)).Value;

    [Fact]
    public async Task A_guest_unused_for_its_sessions_lifetime_and_a_day_goes_with_its_name_tutorials_and_activity()
    {
        var guest = await GuestAsync();
        await _preferences.SaveTutorialsAsync(guest.Id, TutorialProgress.None.WithDone("sc-repo-tutorial-your-first-combat"), Cancel);
        await new AccountActivityLog(_activity, _clock).SignedInAsync(guest.Id, Cancel);
        _clock.Advance(IdleGuests.KeptFor);

        var removed = await Guests().RemoveAsync(_clock.GetUtcNow());

        removed.ShouldBe(1);
        (await _accounts.FindAsync(guest.Id, Cancel)).ShouldBeNull();
        (await _accounts.FindBySubjectAsync(guest.Identities.All[0], Cancel)).ShouldBeNull();
        (await _accounts.FindUsernameAsync(guest.Username, Cancel)).ShouldBeNull();
        (await _activity.FindAsync(guest.Id, Cancel)).ShouldBeNull();
        (await _preferences.GetTutorialsAsync(guest.Id, Cancel)).ShouldBe(TutorialProgress.None);
    }

    [Fact]
    public async Task A_pass_that_could_not_forget_a_removed_guests_settings_leaves_them_for_the_next_pass_to_forget()
    {
        var guest = await GuestAsync();
        await _preferences.SaveTutorialsAsync(guest.Id, TutorialProgress.None.WithDone("sc-repo-tutorial-your-first-combat"), Cancel);
        _clock.Advance(IdleGuests.KeptFor);
        var failing = Substitute.For<IPreferenceStore>();
        failing.ForgetAsync(guest.Id, Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new IOException("out of service (a test)"));
        failing.ListAccountsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<AccountId>>([]));
        var said = new List<string>();

        var first = await new IdleGuests(_accounts, _activity, failing, _clock) { Said = said.Add }.RemoveAsync(_clock.GetUtcNow());
        var left = await _preferences.GetTutorialsAsync(guest.Id, Cancel);
        var second = await new IdleGuests(_accounts, _activity, _preferences, _clock) { Said = said.Add }.RemoveAsync(_clock.GetUtcNow());

        (first, second).ShouldBe((1, 0), string.Join(" | ", said));
        left.Done.ShouldNotBeEmpty();
        said.ShouldHaveSingleItem().ShouldContain($"Account {guest.Id}: a removed guest's settings");
        (await _accounts.FindAsync(guest.Id, Cancel)).ShouldBeNull();
        (await _preferences.GetTutorialsAsync(guest.Id, Cancel)).Done.ShouldBeEmpty();
    }

    [Fact]
    public async Task Accounts_that_cannot_be_listed_remove_no_guest_and_say_so_rather_than_stop_the_pass()
    {
        var failing = Substitute.For<Application.Accounts.Ports.IAccountStore>();
        failing.ListAsync(Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyList<Account>>>(_ => throw new IOException("out of service (a test)"));
        var said = new List<string>();

        var removed = await new IdleGuests(failing, _activity, _preferences, _clock) { Said = said.Add }.RemoveAsync(_clock.GetUtcNow());

        removed.ShouldBe(0);
        said.ShouldHaveSingleItem().ShouldStartWith("  Guests: the accounts could not be listed");
    }

    [Fact]
    public async Task A_guest_used_since_is_kept_its_lifetime_from_that_use()
    {
        var guest = await GuestAsync();
        _clock.Advance(TimeSpan.FromDays(20));
        await new AccountActivityLog(_activity, _clock).SeenAsync(guest.Id, Cancel);
        _clock.Advance(TimeSpan.FromDays(20));

        var removed = await Guests().RemoveAsync(_clock.GetUtcNow());

        removed.ShouldBe(0);
        (await _accounts.FindAsync(guest.Id, Cancel)).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_player_unused_as_long_is_not_a_guest_and_is_kept()
    {
        var mark = (await new AccountRegistry(_accounts, _clock).RegisterAsync(new ExternalIdentity(ExternalIdentity.Google, "1098"), "MarkF", Cancel)).Value;
        _clock.Advance(IdleGuests.KeptFor * 3);

        (await Guests().RemoveAsync(_clock.GetUtcNow())).ShouldBe(0);
        (await _accounts.FindAsync(mark.Id, Cancel)).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_guest_upgraded_by_the_api_while_the_pass_removes_it_keeps_its_account()
    {
        var guest = await GuestAsync();
        _clock.Advance(IdleGuests.KeptFor);
        var google = new ExternalIdentity(ExternalIdentity.Google, "109876543210987654321");
        var upgrading = new UpgradingStore(_accounts, async () =>
        {
            await _accounts.TryBindSubjectAsync(google, guest.Id, Cancel);
            await _accounts.SaveAsync(guest with { Identities = AccountIdentities.Of(google), Username = Username.Create("MarkF").Value }, Cancel);
        });

        var removed = await new IdleGuests(upgrading, _activity, _preferences, _clock).RemoveAsync(_clock.GetUtcNow());

        removed.ShouldBe(0);
        (await _accounts.FindAsync(guest.Id, Cancel)).ShouldNotBeNull().IsGuest.ShouldBeFalse();
        (await _accounts.FindBySubjectAsync(google, Cancel)).ShouldBe(guest.Id);
    }

    [Fact]
    public async Task A_guest_upgraded_between_the_passs_last_read_and_its_removal_keeps_its_tutorials_and_activity()
    {
        var guest = await GuestAsync();
        await _preferences.SaveTutorialsAsync(guest.Id, TutorialProgress.None.WithDone("sc-repo-tutorial-your-first-combat"), Cancel);
        await new AccountActivityLog(_activity, _clock).SignedInAsync(guest.Id, Cancel);
        _clock.Advance(IdleGuests.KeptFor);
        var google = new ExternalIdentity(ExternalIdentity.Google, "109876543210987654321");
        var upgrading = new UpgradingStore(_accounts, async () =>
        {
            await _accounts.TryBindSubjectAsync(google, guest.Id, Cancel);
            await _accounts.SaveAsync(guest with { Identities = AccountIdentities.Of(google), Username = Username.Create("MarkF").Value }, Cancel);
        }, afterReRead: true);

        var removed = await new IdleGuests(upgrading, _activity, _preferences, _clock).RemoveAsync(_clock.GetUtcNow());

        removed.ShouldBe(0);
        (await _accounts.FindAsync(guest.Id, Cancel)).ShouldNotBeNull().IsGuest.ShouldBeFalse();
        (await _preferences.GetTutorialsAsync(guest.Id, Cancel)).Done.ShouldBe(["sc-repo-tutorial-your-first-combat"]);
        (await _activity.FindAsync(guest.Id, Cancel)).ShouldNotBeNull();
    }

    [Fact]
    public async Task What_a_removed_guest_left_behind_is_forgotten_by_the_next_pass_and_a_players_is_kept()
    {
        var gone = AccountId.New();
        await _preferences.SaveTutorialsAsync(gone, TutorialProgress.None.WithDone("sc-repo-tutorial-your-first-combat"), Cancel);
        await _activity.SaveAsync(new AccountActivity(gone, _clock.GetUtcNow(), null), Cancel);
        var mark = (await new AccountRegistry(_accounts, _clock).RegisterAsync(new ExternalIdentity(ExternalIdentity.Google, "1098"), "MarkF", Cancel)).Value;
        await _preferences.SaveTutorialsAsync(mark.Id, TutorialProgress.None.WithDone("sc-repo-tutorial-your-first-combat"), Cancel);

        await Guests().RemoveAsync(_clock.GetUtcNow());

        (await _preferences.GetTutorialsAsync(gone, Cancel)).Done.ShouldBeEmpty();
        (await _activity.FindAsync(gone, Cancel)).ShouldBeNull();
        (await _preferences.GetTutorialsAsync(mark.Id, Cancel)).Done.ShouldBe(["sc-repo-tutorial-your-first-combat"]);
    }

    /// <summary>
    /// The accounts on disk, upgraded by the API the first time the registry reads one to delete it, or, with
    /// <paramref name="afterReRead" />, just after the pass reads the guest again and finds it idle (#1253).
    /// </summary>
    private sealed class UpgradingStore(IAccountStore inner, Func<Task> upgrade, bool afterReRead = false) : IAccountStore
    {
        private Func<Task>? _upgrade = upgrade;

        public async Task<AccountRead?> ReadAsync(AccountId id, CancellationToken cancellationToken = default)
        {
            if (!afterReRead && Interlocked.Exchange(ref _upgrade, null) is { } now)
            {
                await now();
            }

            return await inner.ReadAsync(id, cancellationToken);
        }

        public async Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default)
        {
            var found = await inner.FindAsync(id, cancellationToken);
            if (afterReRead && Interlocked.Exchange(ref _upgrade, null) is { } now)
            {
                await now();
            }

            return found;
        }

        public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default) => inner.ListAsync(cancellationToken);

        public Task SaveAsync(Account account, CancellationToken cancellationToken = default) => inner.SaveAsync(account, cancellationToken);

        public Task<bool> SaveIfUnchangedAsync(Account account, string version, CancellationToken cancellationToken = default) =>
            inner.SaveIfUnchangedAsync(account, version, cancellationToken);

        public Task<bool> DeleteAsync(AccountId id, CancellationToken cancellationToken = default) => inner.DeleteAsync(id, cancellationToken);

        public Task<bool> DeleteIfUnchangedAsync(AccountId id, string version, CancellationToken cancellationToken = default) =>
            inner.DeleteIfUnchangedAsync(id, version, cancellationToken);

        public Task<AccountId?> FindBySubjectAsync(ExternalIdentity identity, CancellationToken cancellationToken = default) => inner.FindBySubjectAsync(identity, cancellationToken);

        public Task<bool> TryBindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default) => inner.TryBindSubjectAsync(identity, account, cancellationToken);

        public Task<bool> UnbindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default) => inner.UnbindSubjectAsync(identity, account, cancellationToken);

        public Task<UsernameReservation?> FindUsernameAsync(Username username, CancellationToken cancellationToken = default) => inner.FindUsernameAsync(username, cancellationToken);

        public Task<bool> TryReserveUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default) => inner.TryReserveUsernameAsync(reservation, cancellationToken);

        public Task<bool> ReleaseUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default) => inner.ReleaseUsernameAsync(reservation, cancellationToken);

        public Task<byte[]> SessionKeyAsync(CancellationToken cancellationToken = default) => inner.SessionKeyAsync(cancellationToken);
    }
}

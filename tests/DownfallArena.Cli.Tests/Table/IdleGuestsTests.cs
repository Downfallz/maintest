using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
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
    public async Task A_pass_that_could_not_forget_a_guests_settings_keeps_the_guest_for_the_next_pass_to_finish()
    {
        var guest = await GuestAsync();
        await _preferences.SaveTutorialsAsync(guest.Id, TutorialProgress.None.WithDone("sc-repo-tutorial-your-first-combat"), Cancel);
        _clock.Advance(IdleGuests.KeptFor);
        var failing = NSubstitute.Substitute.For<Application.Accounts.Ports.IPreferenceStore>();
        failing.ForgetAsync(guest.Id, Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new IOException("out of service (a test)"));
        var said = new List<string>();

        var first = await new IdleGuests(_accounts, _activity, failing, _clock) { Said = said.Add }.RemoveAsync(_clock.GetUtcNow());
        var left = await _accounts.FindAsync(guest.Id, Cancel);
        var second = await new IdleGuests(_accounts, _activity, _preferences, _clock) { Said = said.Add }.RemoveAsync(_clock.GetUtcNow());

        left.ShouldNotBeNull();
        (first, second).ShouldBe((0, 1), string.Join(" | ", said));
        said.ShouldHaveSingleItem().ShouldContain($"Account {guest.Id}");
        (await _preferences.GetTutorialsAsync(guest.Id, Cancel)).ShouldBe(TutorialProgress.None);
        (await _accounts.FindAsync(guest.Id, Cancel)).ShouldBeNull();
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
}

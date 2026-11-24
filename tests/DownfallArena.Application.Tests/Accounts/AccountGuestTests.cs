using DownfallArena.Application.Accounts;
using DownfallArena.Application.Tests.Support;

namespace DownfallArena.Application.Tests.Accounts;

/// <summary>A guest (ADR 0207): an account the host makes without a provider, upgraded by a provider's sign-in.</summary>
public sealed class AccountGuestTests
{
    private static readonly ExternalIdentity Google = new(ExternalIdentity.Google, "109876543210987654321");
    private static readonly ExternalIdentity Discord = new(ExternalIdentity.Discord, "80351110224678912");

    private readonly MemoryAccountStore _store = new();
    private readonly FixedTimeProvider _clock = new(FixedTimeProvider.Default);

    private AccountRegistry Registry(params string[] codes)
    {
        var drawn = new Queue<string>(codes);
        return new AccountRegistry(_store, _clock, guestCode: codes.Length == 0 ? null : () => drawn.Dequeue());
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_guest_is_an_account_under_an_identity_of_the_host_and_a_drawn_name()
    {
        var guest = (await Registry("7KQ2").MakeGuestAsync(Cancel)).Value;

        guest.IsGuest.ShouldBeTrue();
        guest.Username.Value.ShouldBe("Guest-7KQ2");
        var identity = guest.Identities.All.ShouldHaveSingleItem();
        identity.Provider.ShouldBe(ExternalIdentity.Guest);
        _store.Subjects[identity].ShouldBe(guest.Id);
        _store.Names["guest-7kq2"].Account.ShouldBe(guest.Id);
    }

    [Fact]
    public async Task Two_guests_get_two_identities()
    {
        var first = (await Registry().MakeGuestAsync(Cancel)).Value;
        var second = (await Registry().MakeGuestAsync(Cancel)).Value;

        first.Identities.ShouldNotBe(second.Identities);
        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public async Task A_drawn_name_somebody_holds_is_drawn_again()
    {
        var first = (await Registry("7KQ2").MakeGuestAsync(Cancel)).Value;

        var second = (await Registry("7KQ2", "7KQ2", "ABCD").MakeGuestAsync(Cancel)).Value;

        (first.Username.Value, second.Username.Value).ShouldBe(("Guest-7KQ2", "Guest-ABCD"));
    }

    [Fact]
    public async Task A_guest_is_refused_once_every_draw_is_held()
    {
        await Registry("7KQ2").MakeGuestAsync(Cancel);

        var refused = await Registry([.. Enumerable.Repeat("7KQ2", AccountRegistry.GuestDraws)]).MakeGuestAsync(Cancel);

        refused.Error.ShouldBe(AccountErrors.UsernameTaken);
    }

    [Theory]
    [InlineData("Guest-7KQ2")]
    [InlineData("guest-abcd")]
    [InlineData("GUEST_7KQ2")]
    public void A_player_cannot_type_a_guests_name(string typed) =>
        Username.Create(typed).Error.ShouldBe(AccountErrors.UsernameReserved);

    [Fact]
    public void A_name_that_only_starts_with_the_word_is_a_players() =>
        Username.Create("Guestly").IsSuccess.ShouldBeTrue();

    [Fact]
    public async Task Upgrading_keeps_the_id_and_takes_the_providers_identity_and_the_chosen_name()
    {
        var guest = (await Registry("7KQ2").MakeGuestAsync(Cancel)).Value;
        var identity = guest.Identities.All[0];

        var upgraded = (await Registry().UpgradeAsync(guest.Id, Google, "MarkF", Cancel)).Value;

        upgraded.Id.ShouldBe(guest.Id);
        upgraded.IsGuest.ShouldBeFalse();
        upgraded.Identities.All.ShouldBe([Google]);
        upgraded.Username.Value.ShouldBe("MarkF");
        upgraded.CreatedAt.ShouldBe(guest.CreatedAt);
        upgraded.Generation.ShouldBe(guest.Generation + 1);
        (await Registry().FindAsync(Google, Cancel)).ShouldNotBeNull().Id.ShouldBe(guest.Id);
        _store.Subjects.ShouldNotContainKey(identity);
        _store.Names.ShouldNotContainKey("guest-7kq2");
        _store.Names["markf"].Account.ShouldBe(guest.Id);
    }

    [Fact]
    public async Task An_account_that_is_not_a_guest_is_not_upgraded()
    {
        var mark = (await Registry().RegisterAsync(Google, "MarkF", Cancel)).Value;

        var refused = await Registry().UpgradeAsync(mark.Id, Discord, "Bastien", Cancel);

        refused.Error.ShouldBe(AccountErrors.NotAGuest);
    }

    [Fact]
    public async Task An_identity_another_account_holds_does_not_upgrade_a_guest_and_frees_the_name_it_held()
    {
        await Registry().RegisterAsync(Google, "MarkF", Cancel);
        var guest = (await Registry().MakeGuestAsync(Cancel)).Value;

        var refused = await Registry().UpgradeAsync(guest.Id, Google, "Bastien", Cancel);

        refused.Error.ShouldBe(AccountErrors.AlreadyRegistered);
        _store.Accounts[guest.Id].ShouldBe(guest);
        _store.Names.ShouldNotContainKey("bastien");
    }

    [Fact]
    public async Task A_name_somebody_holds_does_not_upgrade_a_guest()
    {
        await Registry().RegisterAsync(Google, "MarkF", Cancel);
        var guest = (await Registry().MakeGuestAsync(Cancel)).Value;

        var refused = await Registry().UpgradeAsync(guest.Id, Discord, "markf", Cancel);

        refused.Error.ShouldBe(AccountErrors.UsernameTaken);
        _store.Accounts[guest.Id].ShouldBe(guest);
    }

    [Fact]
    public async Task An_upgrade_that_stopped_after_holding_its_name_finishes_when_asked_again()
    {
        var guest = (await Registry().MakeGuestAsync(Cancel)).Value;
        _store.Names["markf"] = new UsernameReservation(Username.Create("MarkF").Value, guest.Id, _clock.GetUtcNow());
        _store.Subjects[Google] = guest.Id;

        var upgraded = await Registry().UpgradeAsync(guest.Id, Google, "MarkF", Cancel);

        upgraded.Value.Identities.All.ShouldBe([Google]);
    }

    [Fact]
    public async Task A_guest_adds_no_way_in_but_by_its_upgrade()
    {
        var guest = (await Registry().MakeGuestAsync(Cancel)).Value;

        var refused = await Registry().LinkAsync(guest.Id, Google, cancellationToken: Cancel);

        refused.Error.ShouldBe(AccountErrors.GuestNotAllowed);
        _store.Subjects.ShouldNotContainKey(Google);
    }
}

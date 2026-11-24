using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Hosting;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A guest at the account routes (ADR 0207): <c>POST /api/account/guest</c> makes one and signs it in, counted by
/// address and closed with registration, and a provider's sign-in upgrades it, keeping its id and its tutorials.
/// </summary>
public sealed class AccountGuestApiTests : IDisposable
{
    private const string Tutorial = "sc-repo-tutorial-your-first-combat";
    private static readonly ExternalIdentity Mark = new(ExternalIdentity.Google, "109876543210987654321");

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"accounts-guest-{Guid.CreateVersion7():N}");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 24, 20, 14, 0, TimeSpan.Zero));
    private readonly FileAccountStore _store;
    private readonly FilePreferenceStore _preferences;

    public AccountGuestApiTests()
    {
        _store = new FileAccountStore(_root);
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

    private async Task<AccountApi> ApiAsync(List<string>? said = null) =>
        new(new AccountRegistry(_store, _clock), new AccountSessions(await _store.SessionKeyAsync(Cancel), _clock), AccountsMode.SignIn, [new OneToken()])
        {
            Preferences = _preferences,
            Said = said is null ? _ => { }
            : said.Add,
        };

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private static string? Session(StudioResponse response) =>
        response.Headers?.SingleOrDefault(header => header.Key == "Set-Cookie").Value?.Split(';')[0][(AccountSessions.Cookie.Length + 1)..];

    private static AccountRequest From(string address, string? session = null) => new(session, Secure: true, address);

    private static async Task<(JsonElement Account, AccountRequest Session)> GuestAsync(AccountApi api, string address = "203.0.113.7")
    {
        var made = await api.HandleAsync("POST", AccountApi.GuestRoute, "", From(address));
        made.Status.ShouldBe(200, Encoding.UTF8.GetString(made.Body));
        return (Json(made).GetProperty("account"), From(address, Session(made)));
    }

    [Fact]
    public async Task A_visitor_made_a_guest_is_signed_in_under_a_drawn_name_and_told_it_is_a_guest()
    {
        var api = await ApiAsync();

        var (account, session) = await GuestAsync(api);
        var me = Json(await api.HandleAsync("GET", AccountApi.Prefix, "", session)).GetProperty("account");

        account.GetProperty("username").GetString().ShouldStartWith(Username.GuestPrefix);
        account.GetProperty("guest").GetBoolean().ShouldBeTrue();
        me.GetProperty("id").GetString().ShouldBe(account.GetProperty("id").GetString());
        me.GetProperty("guest").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task A_guest_asking_again_is_answered_the_guest_it_is()
    {
        var api = await ApiAsync();
        var (account, session) = await GuestAsync(api);

        var again = Json(await api.HandleAsync("POST", AccountApi.GuestRoute, "", session)).GetProperty("account");

        again.GetProperty("id").GetString().ShouldBe(account.GetProperty("id").GetString());
        (await _store.ListAsync(Cancel)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task One_address_makes_its_count_of_guests_an_hour_and_is_then_asked_to_wait()
    {
        var api = await ApiAsync();
        for (var made = 0; made < GuestMaking.PerAddress; made++)
        {
            await GuestAsync(api);
        }

        var refused = await api.HandleAsync("POST", AccountApi.GuestRoute, "", From("203.0.113.7"));
        var elsewhere = await api.HandleAsync("POST", AccountApi.GuestRoute, "", From("198.51.100.4"));
        _clock.Advance(GuestMaking.Window);
        var later = await api.HandleAsync("POST", AccountApi.GuestRoute, "", From("203.0.113.7"));

        refused.Status.ShouldBe(429);
        Json(refused).GetProperty("error").GetString().ShouldBe("Account.TooManyGuests");
        refused.Headers!.ShouldContain(header => header.Key == "Retry-After");
        (elsewhere.Status, later.Status).ShouldBe((200, 200));
    }

    [Fact]
    public void The_host_makes_its_count_of_guests_an_hour_from_every_address_together()
    {
        var making = new GuestMaking();
        var now = _clock.GetUtcNow();
        for (var made = 0; made < GuestMaking.PerHost; made++)
        {
            making.Take($"198.51.{made / 250}.{made % 250}", now).ShouldBeNull();
        }

        making.Take("203.0.113.7", now).ShouldNotBeNull();
        making.Take("203.0.113.7", now + GuestMaking.Window).ShouldBeNull();
    }

    [Fact]
    public async Task No_guest_is_made_while_registration_is_closed()
    {
        var api = await ApiAsync();
        api.Settings = new HostSettingsCache(new FileHostSettingsStore(_root), _clock);
        await api.Settings.SaveAsync(new HostSettings(RegistrationOpen: false, Notice: null, "mark", _clock.GetUtcNow()));

        var refused = await api.HandleAsync("POST", AccountApi.GuestRoute, "", From("203.0.113.7"));

        refused.Status.ShouldBe(409);
        Json(refused).GetProperty("error").GetString().ShouldBe(AccountErrors.RegistrationClosed.Code);
        (await _store.ListAsync(Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_guest_choosing_a_name_after_a_providers_sign_in_keeps_its_id_and_its_tutorials()
    {
        var said = new List<string>();
        var api = await ApiAsync(said);
        var (account, session) = await GuestAsync(api);
        var id = account.GetProperty("id").GetString()!;
        await _preferences.SaveTutorialsAsync(SharedKernel.Identifiers.AccountId.Parse(id), TutorialProgress.None.WithDone(Tutorial), Cancel);

        var ticket = Json(await api.HandleAsync("POST", "/api/account/google", """{"credential":"mark"}""", session)).GetProperty("ticket").GetString();
        var upgraded = await api.HandleAsync("POST", AccountApi.Prefix, JsonSerializer.Serialize(new { ticket, username = "MarkF" }), session);
        var me = Json(await api.HandleAsync("GET", AccountApi.Prefix, "", From("203.0.113.7", Session(upgraded)))).GetProperty("account");

        upgraded.Status.ShouldBe(200, Encoding.UTF8.GetString(upgraded.Body));
        (me.GetProperty("id").GetString(), me.GetProperty("username").GetString(), me.GetProperty("guest").GetBoolean()).ShouldBe((id, "MarkF", false));
        (await api.TutorialsAsync(SharedKernel.Identifiers.AccountId.Parse(id), Cancel))!.StateOf(Tutorial).ShouldBe(TutorialState.Done);
        (await _store.ListAsync(Cancel)).ShouldHaveSingleItem().Identities.All.ShouldBe([Mark]);
        said.ShouldContain($"  Guest upgraded (google): account {id}.");
    }

    [Fact]
    public async Task The_guests_cookie_no_longer_signs_in_once_it_is_upgraded()
    {
        var api = await ApiAsync();
        var (_, session) = await GuestAsync(api);
        var ticket = Json(await api.HandleAsync("POST", "/api/account/google", """{"credential":"mark"}""", session)).GetProperty("ticket").GetString();

        await api.HandleAsync("POST", AccountApi.Prefix, JsonSerializer.Serialize(new { ticket, username = "MarkF" }), session);
        var me = Json(await api.HandleAsync("GET", AccountApi.Prefix, "", session));

        me.GetProperty("account").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_guest_signing_in_to_an_account_it_already_has_is_signed_in_to_it_and_the_guest_removed()
    {
        var api = await ApiAsync();
        var mark = (await new AccountRegistry(_store, _clock).RegisterAsync(Mark, "MarkF", Cancel)).Value;
        var (account, session) = await GuestAsync(api);

        var signedIn = await api.HandleAsync("POST", "/api/account/google", """{"credential":"mark"}""", session);

        Json(signedIn).GetProperty("account").GetProperty("id").GetString().ShouldBe(mark.Id.Value);
        (await _store.FindAsync(SharedKernel.Identifiers.AccountId.Parse(account.GetProperty("id").GetString()!), Cancel)).ShouldBeNull();
        (await _store.ListAsync(Cancel)).ShouldHaveSingleItem().Id.ShouldBe(mark.Id);
    }

    [Fact]
    public async Task An_upgrade_asked_again_after_its_answer_was_lost_signs_in_the_upgraded_account()
    {
        var api = await ApiAsync();
        var (account, session) = await GuestAsync(api);
        var ticket = Json(await api.HandleAsync("POST", "/api/account/google", """{"credential":"mark"}""", session)).GetProperty("ticket").GetString();
        var body = JsonSerializer.Serialize(new { ticket, username = "MarkF" });
        await api.HandleAsync("POST", AccountApi.Prefix, body, session);

        var again = await api.HandleAsync("POST", AccountApi.Prefix, body, session);

        again.Status.ShouldBe(200, Encoding.UTF8.GetString(again.Body));
        Json(again).GetProperty("account").GetProperty("id").GetString().ShouldBe(account.GetProperty("id").GetString());
        Session(again).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_sign_in_whose_guest_was_upgraded_meanwhile_into_that_account_keeps_the_account()
    {
        var api = await ApiAsync();
        var (account, session) = await GuestAsync(api);
        var id = SharedKernel.Identifiers.AccountId.Parse(account.GetProperty("id").GetString()!);
        await api.HandleAsync("GET", AccountApi.Prefix, "", session);
        // The upgrade was written, but its answer was lost: the API still holds the guest it read.
        await new AccountRegistry(_store, _clock).UpgradeAsync(id, Mark, "MarkF", Cancel);

        var signedIn = await api.HandleAsync("POST", "/api/account/google", """{"credential":"mark"}""", session);

        signedIn.Status.ShouldBe(200, Encoding.UTF8.GetString(signedIn.Body));
        (await _store.FindAsync(id, Cancel)).ShouldNotBeNull().Username.Value.ShouldBe("MarkF");
    }

    [Fact]
    public async Task A_player_signed_in_is_not_made_a_guest()
    {
        var api = await ApiAsync();
        var ticket = Json(await api.HandleAsync("POST", "/api/account/google", """{"credential":"mark"}""", From("203.0.113.7"))).GetProperty("ticket").GetString();
        var session = From("203.0.113.7", Session(await api.HandleAsync("POST", AccountApi.Prefix, JsonSerializer.Serialize(new { ticket, username = "MarkF" }), From("203.0.113.7"))));

        var answer = Json(await api.HandleAsync("POST", AccountApi.GuestRoute, "", session)).GetProperty("account");

        (answer.GetProperty("username").GetString(), answer.GetProperty("guest").GetBoolean()).ShouldBe(("MarkF", false));
        (await _store.ListAsync(Cancel)).Count.ShouldBe(1);
    }

    /// <summary>Google, as far as these tests need it: the credential "mark" is Mark's, every other one is nobody's.</summary>
    private sealed class OneToken : ISignInProvider
    {
        public string Name => ExternalIdentity.Google;

        public string? ClientId => "1234-test.apps.googleusercontent.com";

        public Task<ExternalIdentity?> SignInAsync(SignInProof proof, CancellationToken cancellationToken = default) =>
            Task.FromResult(proof["credential"] == "mark" ? Mark : (ExternalIdentity?)null);
    }
}

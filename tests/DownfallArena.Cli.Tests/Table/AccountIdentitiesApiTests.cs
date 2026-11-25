using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// Settings' ways to sign in (sign-in methods plan, P4, ADR 0204): a signed-in player lists the providers of the
/// account, adds one through that provider's own sign-in, and removes one.
/// </summary>
public sealed class AccountIdentitiesApiTests : IDisposable
{
    private const string Route = "/api/account/identities";
    private static readonly ExternalIdentity Mark = new(ExternalIdentity.Google, "109876543210987654321");
    private static readonly ExternalIdentity MarkOnDiscord = new(ExternalIdentity.Discord, "80351110224678912");

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"accounts-ways-{Guid.CreateVersion7():N}");
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 11, 23, 20, 14, 0, TimeSpan.Zero));
    private readonly UnbindingStore _store;

    public AccountIdentitiesApiTests()
    {
        _store = new UnbindingStore(new FileAccountStore(_root));
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private AccountRegistry Registry() => new(_store, _clock);

    private async Task<AccountApi> ApiAsync(params ISignInProvider[] more) =>
        new(Registry(), new AccountSessions(await _store.SessionKeyAsync(Cancel), _clock), AccountsMode.SignIn, [new Named(ExternalIdentity.Google), new Named(ExternalIdentity.Discord), .. more]);

    private static AccountRequest Signed(string? session) => new(session, Secure: true);

    private static AccountRequest Nobody => new(null, Secure: true);

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private static string Error(StudioResponse response) => Json(response).GetProperty("error").GetString()!;

    private static string? Session(StudioResponse response)
    {
        var cookie = response.Headers?.SingleOrDefault(header => header.Key == "Set-Cookie").Value;
        return cookie?.Split(';')[0][(AccountSessions.Cookie.Length + 1)..];
    }

    private static string[] Providers(StudioResponse response) =>
        [.. Json(response).GetProperty("identities").EnumerateArray().Select(way => way.GetProperty("provider").GetString()!)];

    // The proof of Mark's recent sign-in, as the sign-in's answer hands it to the page; empty before he signs in.
    private string _recent = "";

    /// <summary>A provider's proof naming the subject, with the recent sign-in the page sends beside it.</summary>
    private string Proof(string subject) => $$"""{"subject":"{{subject}}","recent":"{{_recent}}"}""";

    /// <summary>What a removal sends: the recent sign-in alone.</summary>
    private string Recent() => $$"""{"recent":"{{_recent}}"}""";

    /// <summary>Mark registered with Google, and a session of his from that sign-in, which also proves it recent.</summary>
    private async Task<string> MarkSignedInAsync(AccountApi api)
    {
        await Registry().RegisterAsync(Mark, "MarkF", Cancel);
        var signedIn = await api.HandleAsync("POST", "/api/account/google", Proof(Mark.Subject), Nobody);
        _recent = Json(signedIn).GetProperty("recent").GetString()!;
        return Session(signedIn)!;
    }

    [Fact]
    public async Task A_signed_in_player_reads_the_providers_of_the_account_and_not_their_identifiers()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);

        var answer = await api.HandleAsync("GET", Route, "", Signed(session));

        answer.Status.ShouldBe(200);
        Providers(answer).ShouldBe(["google"]);
        Encoding.UTF8.GetString(answer.Body).ShouldNotContain(Mark.Subject);
    }

    [Fact]
    public async Task The_host_says_which_ways_can_be_removed_the_only_one_never()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);

        var alone = Json(await api.HandleAsync("GET", Route, "", Signed(session))).GetProperty("identities");
        var both = Json(await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session))).GetProperty("identities");

        alone.EnumerateArray().Select(way => way.GetProperty("removable").GetBoolean()).ShouldBe([false]);
        both.EnumerateArray().Select(way => way.GetProperty("removable").GetBoolean()).ShouldBe([true, true]);
    }

    [Theory]
    [InlineData("GET", Route)]
    [InlineData("POST", Route + "/discord")]
    [InlineData("DELETE", Route + "/google")]
    public async Task Nobody_signed_in_reaches_no_way_to_sign_in(string method, string path)
    {
        var api = await ApiAsync();

        var answer = await api.HandleAsync(method, path, Proof(MarkOnDiscord.Subject), Nobody);

        answer.Status.ShouldBe(401);
        Error(answer).ShouldBe("Account.SignInRequired");
    }

    [Fact]
    public async Task Adding_a_provider_lets_it_sign_in_to_the_same_account_and_keeps_the_session()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);

        var added = await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));

        added.Status.ShouldBe(200);
        Providers(added).ShouldBe(["google", "discord"]);
        var throughDiscord = await api.HandleAsync("POST", "/api/account/discord", Proof(MarkOnDiscord.Subject), Nobody);
        Json(throughDiscord).GetProperty("account").GetProperty("username").GetString().ShouldBe("MarkF");
        Json(await api.HandleAsync("GET", "/api/account", "", Signed(session))).GetProperty("account").ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public async Task Each_way_added_refused_or_removed_is_a_console_line_naming_the_account_and_not_the_identity()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        var id = (await Registry().FindAsync(Mark, Cancel))!.Id;
        var said = new List<string>();
        api.Said = said.Add;

        await api.HandleAsync("POST", Route + "/discord", Recent(), Signed(session));
        await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));
        await api.HandleAsync("POST", Route + "/google", Proof("305555555555555555555"), Signed(session));
        await api.HandleAsync("DELETE", Route + "/discord", Recent(), Signed(session));

        said.ShouldBe([
            $"  Way in refused (discord) for account {id}: 401.",
            $"  Way in added (discord) to account {id}.",
            $"  Way in refused (google) for account {id}: Account.ProviderLinked.",
            $"  Way in removed (discord) from account {id}.",
        ]);
    }

    [Fact]
    public async Task A_session_without_a_recent_sign_in_changes_no_way_in()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        _recent = "";

        var added = await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));
        var removed = await api.HandleAsync("DELETE", Route + "/google", "{}", Signed(session));

        added.Status.ShouldBe(403);
        Error(added).ShouldBe("Account.SignInAgain");
        removed.Status.ShouldBe(403);
        Error(removed).ShouldBe("Account.SignInAgain");
        Providers(await api.HandleAsync("GET", Route, "", Signed(session))).ShouldBe(["google"]);
    }

    [Fact]
    public async Task A_sign_in_older_than_ten_minutes_is_not_recent()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        _clock.Advance(AccountSessions.RecentFor);

        var answer = await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));

        answer.Status.ShouldBe(403);
        Error(answer).ShouldBe("Account.SignInAgain");
    }

    [Fact]
    public async Task Another_accounts_recent_sign_in_does_not_count_for_this_one()
    {
        var api = await ApiAsync();
        await Registry().RegisterAsync(MarkOnDiscord, "Bastien", Cancel);
        var bastien = await api.HandleAsync("POST", "/api/account/discord", Proof(MarkOnDiscord.Subject), Nobody);
        var bastiensRecent = Json(bastien).GetProperty("recent").GetString()!;
        var session = await MarkSignedInAsync(api);
        _recent = bastiensRecent;

        var answer = await api.HandleAsync("POST", Route + "/discord", Proof("123"), Signed(session));

        answer.Status.ShouldBe(403);
        Error(answer).ShouldBe("Account.SignInAgain");
    }

    [Fact]
    public async Task A_sign_out_everywhere_while_the_provider_is_asked_adds_nothing()
    {
        var registry = Registry();
        var slow = new SignsOutMeanwhile("other", registry);
        var api = await ApiAsync(slow);
        var session = await MarkSignedInAsync(api);
        slow.Account = (await registry.FindAsync(Mark, Cancel))!.Id;

        var answer = await api.HandleAsync("POST", Route + "/other", Proof("7"), Signed(session));

        answer.Status.ShouldBe(401);
        Error(answer).ShouldBe("Account.SignInRequired");
        (await registry.FindAsync(new ExternalIdentity("other", "7"), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task Adding_a_way_spends_the_addresss_sign_in_count_as_a_sign_in_does()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        var fromHome = new AccountRequest(session, Secure: true, Address: "203.0.113.7");
        for (var posted = 0; posted < SignInAttempts.PerAddress; posted++)
        {
            (await api.HandleAsync("POST", Route + "/discord", Recent(), fromHome)).Status.ShouldBe(401);
        }

        var answer = await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), fromHome);

        answer.Status.ShouldBe(429);
        Error(answer).ShouldBe("Account.TooManySignIns");
        Providers(await api.HandleAsync("GET", Route, "", Signed(session))).ShouldBe(["google"]);
    }

    [Fact]
    public async Task A_provider_the_host_does_not_offer_cannot_be_added()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);

        var answer = await api.HandleAsync("POST", Route + "/microsoft", Proof("anyone"), Signed(session));

        answer.Status.ShouldBe(404);
        Error(answer).ShouldBe("Account.NoProvider");
    }

    [Fact]
    public async Task A_proof_the_provider_refuses_adds_nothing()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);

        var answer = await api.HandleAsync("POST", Route + "/discord", Recent(), Signed(session));

        answer.Status.ShouldBe(401);
        Error(answer).ShouldBe("Account.SignInRejected");
        Providers(await api.HandleAsync("GET", Route, "", Signed(session))).ShouldBe(["google"]);
    }

    [Fact]
    public async Task A_provider_that_names_another_providers_identity_adds_nothing()
    {
        var api = await ApiAsync(new Impostor("other"));
        var session = await MarkSignedInAsync(api);

        var answer = await api.HandleAsync("POST", Route + "/other", Proof("x"), Signed(session));

        answer.Status.ShouldBe(401);
        Error(answer).ShouldBe("Account.SignInRejected");
    }

    [Fact]
    public async Task A_provider_out_of_reach_is_unavailable_and_adds_nothing()
    {
        var api = await ApiAsync(new Unreachable("other"));
        var session = await MarkSignedInAsync(api);

        var answer = await api.HandleAsync("POST", Route + "/other", Proof("x"), Signed(session));

        answer.Status.ShouldBe(503);
        Error(answer).ShouldBe("Account.ProviderUnavailable");
        (answer.Headers ?? []).ShouldNotContain(header => header.Key == "Retry-After");
    }

    [Fact]
    public async Task A_provider_busy_for_a_moment_says_how_long_to_wait_before_adding_it_again()
    {
        var api = await ApiAsync(new Unreachable("other") { RetryAfter = TimeSpan.FromSeconds(2) });
        var session = await MarkSignedInAsync(api);

        var answer = await api.HandleAsync("POST", Route + "/other", Proof("x"), Signed(session));

        answer.Status.ShouldBe(503);
        Error(answer).ShouldBe("Account.ProviderUnavailable");
        answer.Headers!.Single(header => header.Key == "Retry-After").Value.ShouldBe("2");
    }

    [Fact]
    public async Task A_way_in_another_account_uses_is_refused_as_taken()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        await Registry().RegisterAsync(MarkOnDiscord, "Bastien", Cancel);

        var answer = await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));

        answer.Status.ShouldBe(409);
        Error(answer).ShouldBe("Account.IdentityTaken");
    }

    [Fact]
    public async Task A_second_way_in_through_a_provider_the_account_already_uses_is_refused()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);

        var answer = await api.HandleAsync("POST", Route + "/google", Proof("305555555555555555555"), Signed(session));

        answer.Status.ShouldBe(409);
        Error(answer).ShouldBe("Account.ProviderLinked");
    }

    [Fact]
    public async Task Removing_a_way_in_signs_out_every_other_session_and_keeps_this_one()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));
        var elsewhere = Session(await api.HandleAsync("POST", "/api/account/discord", Proof(MarkOnDiscord.Subject), Nobody));

        var removed = await api.HandleAsync("DELETE", Route + "/discord", Recent(), Signed(session));

        removed.Status.ShouldBe(200);
        Providers(removed).ShouldBe(["google"]);
        Json(await api.HandleAsync("GET", "/api/account", "", Signed(elsewhere))).GetProperty("account").ValueKind.ShouldBe(JsonValueKind.Null);
        Json(await api.HandleAsync("GET", "/api/account", "", Signed(session))).GetProperty("account").ValueKind.ShouldBe(JsonValueKind.Null);
        Json(await api.HandleAsync("GET", "/api/account", "", Signed(Session(removed)))).GetProperty("account").GetProperty("username").GetString().ShouldBe("MarkF");
    }

    [Fact]
    public async Task Removing_a_way_in_forgets_the_accounts_devices()
    {
        var api = await ApiAsync();
        var devices = new AccountDevices(new FileDeviceStore(_root), _clock);
        api.Devices = devices;
        var session = await MarkSignedInAsync(api);
        var id = (await Registry().FindAsync(Mark, Cancel))!.Id;
        await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));
        await devices.AddAsync(id, new PushDevice("https://fcm.googleapis.com/fcm/send/lent-phone", "key", "auth", _clock.GetUtcNow()), Cancel);

        await api.HandleAsync("DELETE", Route + "/discord", Recent(), Signed(session));

        (await devices.ListAsync(id, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_removed_way_in_no_longer_reaches_the_account()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));

        await api.HandleAsync("DELETE", Route + "/discord", Recent(), Signed(session));

        Json(await api.HandleAsync("POST", "/api/account/discord", Proof(MarkOnDiscord.Subject), Nobody)).GetProperty("needsUsername").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task A_way_added_while_every_write_of_the_account_loses_to_another_process_is_unavailable_and_not_a_crash()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        _store.RefuseEveryAccountWrite = true;

        var answer = await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));

        answer.Status.ShouldBe(503);
        Error(answer).ShouldBe("Account.Unavailable");
        _store.RefusedWrites.ShouldBe(5);
        _store.RefuseEveryAccountWrite = false;
        Providers(await api.HandleAsync("GET", Route, "", Signed(session))).ShouldBe(["google"]);
    }

    [Fact]
    public async Task A_removal_stopped_before_its_binding_went_is_finished_by_removing_the_way_again()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));
        _store.FailNextUnbinding = true;
        var stopped = await api.HandleAsync("DELETE", Route + "/discord", Recent(), Signed(session));
        var again = await MarkSignedInAsync(api);

        var removed = await api.HandleAsync("DELETE", Route + "/discord", Recent(), Signed(again));

        stopped.Status.ShouldBe(503);
        removed.Status.ShouldBe(200);
        Providers(removed).ShouldBe(["google"]);
        (await _store.FindBySubjectAsync(MarkOnDiscord, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task The_way_in_the_account_registered_with_can_go_once_another_is_added()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));

        var removed = await api.HandleAsync("DELETE", Route + "/google", Recent(), Signed(session));

        Providers(removed).ShouldBe(["discord"]);
        Json(await api.HandleAsync("POST", "/api/account/discord", Proof(MarkOnDiscord.Subject), Nobody)).GetProperty("account").GetProperty("username").GetString().ShouldBe("MarkF");
    }

    [Fact]
    public async Task The_last_way_in_cannot_be_removed()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);

        var answer = await api.HandleAsync("DELETE", Route + "/google", Recent(), Signed(session));

        answer.Status.ShouldBe(409);
        Error(answer).ShouldBe("Account.LastIdentity");
        Session(answer).ShouldBeNull();
    }

    [Fact]
    public async Task A_provider_the_account_does_not_use_cannot_be_removed()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);

        var answer = await api.HandleAsync("DELETE", Route + "/discord", Recent(), Signed(session));

        answer.Status.ShouldBe(404);
        Error(answer).ShouldBe("Account.IdentityNotLinked");
    }

    [Fact]
    public async Task A_way_in_whose_provider_the_host_no_longer_offers_is_listed_and_can_be_removed()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        var account = await Registry().FindAsync(Mark, Cancel);
        await Registry().LinkAsync(account!.Id, new ExternalIdentity("retired", "1"), cancellationToken: Cancel);

        Providers(await api.HandleAsync("GET", Route, "", Signed(session))).ShouldBe(["google", "retired"]);
        var removed = await api.HandleAsync("DELETE", Route + "/retired", Recent(), Signed(session));

        removed.Status.ShouldBe(200);
        Providers(removed).ShouldBe(["google"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>A provider that signs in whoever its posted <c>subject</c> names: the proof is the identity.</summary>
    private sealed class Named(string name) : ISignInProvider
    {
        public string Name => name;

        public string? ClientId => $"{name}-client";

        public Task<ExternalIdentity?> SignInAsync(SignInProof proof, CancellationToken cancellationToken = default) =>
            Task.FromResult(proof["subject"] is { Length: > 0 } subject ? new ExternalIdentity(name, subject) : (ExternalIdentity?)null);
    }

    /// <summary>A provider that answers Mark's Google identity whatever it is posted: one provider passing for another.</summary>
    private sealed class Impostor(string name) : ISignInProvider
    {
        public string Name => name;

        public string? ClientId => null;

        public Task<ExternalIdentity?> SignInAsync(SignInProof proof, CancellationToken cancellationToken = default) => Task.FromResult<ExternalIdentity?>(Mark);
    }

    /// <summary>A provider whose answer comes after the account signed out everywhere, from another device.</summary>
    private sealed class SignsOutMeanwhile(string name, AccountRegistry registry) : ISignInProvider
    {
        public SharedKernel.Identifiers.AccountId? Account { get; set; }

        public string Name => name;

        public string? ClientId => null;

        public async Task<ExternalIdentity?> SignInAsync(SignInProof proof, CancellationToken cancellationToken = default)
        {
            await registry.SignOutEverywhereAsync(Account!.Value, cancellationToken);
            return new ExternalIdentity(name, proof["subject"]!);
        }
    }

    /// <summary>A provider that cannot be asked right now.</summary>
    private sealed class Unreachable(string name) : ISignInProvider
    {
        public string Name => name;

        public string? ClientId => null;

        /// <summary>How soon it says to come back, as a provider whose places at once are all taken.</summary>
        public TimeSpan? RetryAfter { get; init; }

        public Task<ExternalIdentity?> SignInAsync(SignInProof proof, CancellationToken cancellationToken = default) =>
            throw new SignInUnavailableException("out of reach") { RetryAfter = RetryAfter };
    }

    /// <summary>The accounts on disk, whose next removal of a binding fails once <see cref="FailNextUnbinding" /> is set: the store out of service.</summary>
    private sealed class UnbindingStore(IAccountStore inner) : IAccountStore
    {
        public bool FailNextUnbinding { get; set; }

        /// <summary>Refuses every conditional write of an account, as when another process always writes it first.</summary>
        public bool RefuseEveryAccountWrite { get; set; }

        public int RefusedWrites { get; private set; }

        public Task<bool> UnbindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default)
        {
            if (FailNextUnbinding)
            {
                FailNextUnbinding = false;
                throw new IOException("The account store is out of service (a test).");
            }

            return inner.UnbindSubjectAsync(identity, account, cancellationToken);
        }

        public Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default) => inner.FindAsync(id, cancellationToken);

        public Task<AccountRead?> ReadAsync(AccountId id, CancellationToken cancellationToken = default) => inner.ReadAsync(id, cancellationToken);

        public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default) => inner.ListAsync(cancellationToken);

        public Task SaveAsync(Account account, CancellationToken cancellationToken = default) => inner.SaveAsync(account, cancellationToken);

        public Task<bool> SaveIfUnchangedAsync(Account account, string version, CancellationToken cancellationToken = default)
        {
            if (RefuseEveryAccountWrite)
            {
                RefusedWrites++;
                return Task.FromResult(false);
            }

            return inner.SaveIfUnchangedAsync(account, version, cancellationToken);
        }

        public Task<bool> DeleteAsync(AccountId id, CancellationToken cancellationToken = default) => inner.DeleteAsync(id, cancellationToken);

        public Task<bool> DeleteIfUnchangedAsync(AccountId id, string version, CancellationToken cancellationToken = default) =>
            inner.DeleteIfUnchangedAsync(id, version, cancellationToken);

        public Task<AccountId?> FindBySubjectAsync(ExternalIdentity identity, CancellationToken cancellationToken = default) => inner.FindBySubjectAsync(identity, cancellationToken);

        public Task<bool> TryBindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default) => inner.TryBindSubjectAsync(identity, account, cancellationToken);

        public Task<SubjectRead?> ReadSubjectAsync(ExternalIdentity identity, CancellationToken cancellationToken = default) => inner.ReadSubjectAsync(identity, cancellationToken);

        public Task<string?> BindSubjectAsync(ExternalIdentity identity, AccountId account, string? over, CancellationToken cancellationToken = default) =>
            inner.BindSubjectAsync(identity, account, over, cancellationToken);

        public Task<bool> UnbindSubjectIfUnchangedAsync(ExternalIdentity identity, string version, CancellationToken cancellationToken = default) =>
            inner.UnbindSubjectIfUnchangedAsync(identity, version, cancellationToken);

        public Task<UsernameReservation?> FindUsernameAsync(Username username, CancellationToken cancellationToken = default) => inner.FindUsernameAsync(username, cancellationToken);

        public Task<bool> TryReserveUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default) => inner.TryReserveUsernameAsync(reservation, cancellationToken);

        public Task<bool> ReleaseUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default) => inner.ReleaseUsernameAsync(reservation, cancellationToken);

        public Task<byte[]> SessionKeyAsync(CancellationToken cancellationToken = default) => inner.SessionKeyAsync(cancellationToken);
    }

    private sealed class FixedClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}

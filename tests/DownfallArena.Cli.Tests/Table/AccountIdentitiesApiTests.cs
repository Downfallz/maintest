using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;

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
    private readonly FileAccountStore _store;

    public AccountIdentitiesApiTests()
    {
        _store = new FileAccountStore(_root);
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

    private static string Proof(string subject) => $$"""{"subject":"{{subject}}"}""";

    /// <summary>Mark registered with Google, and a session of his from that sign-in.</summary>
    private async Task<string> MarkSignedInAsync(AccountApi api)
    {
        await Registry().RegisterAsync(Mark, "MarkF", Cancel);
        return Session(await api.HandleAsync("POST", "/api/account/google", Proof(Mark.Subject), Nobody))!;
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

        var answer = await api.HandleAsync("POST", Route + "/discord", "{}", Signed(session));

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

        var removed = await api.HandleAsync("DELETE", Route + "/discord", "", Signed(session));

        removed.Status.ShouldBe(200);
        Providers(removed).ShouldBe(["google"]);
        Json(await api.HandleAsync("GET", "/api/account", "", Signed(elsewhere))).GetProperty("account").ValueKind.ShouldBe(JsonValueKind.Null);
        Json(await api.HandleAsync("GET", "/api/account", "", Signed(session))).GetProperty("account").ValueKind.ShouldBe(JsonValueKind.Null);
        Json(await api.HandleAsync("GET", "/api/account", "", Signed(Session(removed)))).GetProperty("account").GetProperty("username").GetString().ShouldBe("MarkF");
    }

    [Fact]
    public async Task A_removed_way_in_no_longer_reaches_the_account()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));

        await api.HandleAsync("DELETE", Route + "/discord", "", Signed(session));

        Json(await api.HandleAsync("POST", "/api/account/discord", Proof(MarkOnDiscord.Subject), Nobody)).GetProperty("needsUsername").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task The_way_in_the_account_registered_with_can_go_once_another_is_added()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        await api.HandleAsync("POST", Route + "/discord", Proof(MarkOnDiscord.Subject), Signed(session));

        var removed = await api.HandleAsync("DELETE", Route + "/google", "", Signed(session));

        Providers(removed).ShouldBe(["discord"]);
        Json(await api.HandleAsync("POST", "/api/account/discord", Proof(MarkOnDiscord.Subject), Nobody)).GetProperty("account").GetProperty("username").GetString().ShouldBe("MarkF");
    }

    [Fact]
    public async Task The_last_way_in_cannot_be_removed()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);

        var answer = await api.HandleAsync("DELETE", Route + "/google", "", Signed(session));

        answer.Status.ShouldBe(409);
        Error(answer).ShouldBe("Account.LastIdentity");
        Session(answer).ShouldBeNull();
    }

    [Fact]
    public async Task A_provider_the_account_does_not_use_cannot_be_removed()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);

        var answer = await api.HandleAsync("DELETE", Route + "/discord", "", Signed(session));

        answer.Status.ShouldBe(404);
        Error(answer).ShouldBe("Account.IdentityNotLinked");
    }

    [Fact]
    public async Task A_way_in_whose_provider_the_host_no_longer_offers_is_listed_and_can_be_removed()
    {
        var api = await ApiAsync();
        var session = await MarkSignedInAsync(api);
        var account = await Registry().FindAsync(Mark, Cancel);
        await Registry().LinkAsync(account!.Id, new ExternalIdentity("retired", "1"), Cancel);

        Providers(await api.HandleAsync("GET", Route, "", Signed(session))).ShouldBe(["google", "retired"]);
        var removed = await api.HandleAsync("DELETE", Route + "/retired", "", Signed(session));

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

    /// <summary>A provider that cannot be asked right now.</summary>
    private sealed class Unreachable(string name) : ISignInProvider
    {
        public string Name => name;

        public string? ClientId => null;

        public Task<ExternalIdentity?> SignInAsync(SignInProof proof, CancellationToken cancellationToken = default) =>
            throw new SignInUnavailableException("out of reach");
    }

    private sealed class FixedClock(DateTimeOffset start) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => start;
    }
}

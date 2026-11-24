using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Hosting;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A guest at the host (ADR 0207): it reaches the routes <see cref="GuestRoutes" /> lists and the seat of its own
/// tutorial, and every other route answers <c>403 Account.GuestNotAllowed</c> before the route itself is asked.
/// </summary>
public sealed class TableServerGuestTests : IAsyncDisposable
{
    private const string Tutorial = "sc-repo-tutorial-your-first-combat";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"server-guest-{Guid.CreateVersion7():N}");
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry;
    private readonly CancellationTokenSource _stopping = new();
    private readonly HttpClient _client = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
    private readonly ScenarioPractices _practices;
    private readonly TableServer _server;
    private readonly AccountApi _accounts;
    private readonly Task _serving;
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly SwitchedStore _store;

    public TableServerGuestTests()
    {
        _registry = _hosted.Registry();
        _store = new SwitchedStore(new FileAccountStore(_root));
        _accounts = new AccountApi(
            new AccountRegistry(_store, _clock),
            new AccountSessions(_store.SessionKeyAsync().GetAwaiter().GetResult(), _clock),
            AccountsMode.Development)
        {
            Said = _ => { },
            Preferences = new FilePreferenceStore(_root),
        };
        var shipped = RepositoryScenarios.Read(Path.Combine(AppContext.BaseDirectory, "scenarios"));
        foreach (var one in shipped.All)
        {
            _hosted.Composer.Carry(one.Document.Content, one.Document.Schema!.Value);
        }

        var scenarios = new ScenarioStore(new FileArtifactStore(Path.Combine(_root, "scenarios")), TimeProvider.System, shipped);
        _practices = new ScenarioPractices(_hosted.Composer, scenarios, TimeProvider.System);
        var admin = new AdminApi(_registry, _hosted.Composer, OperatorGate.BehindPlatform(), TimeProvider.System, "Rules 2 creatures (a test)", _hosted.Stored) { Accounts = _accounts };
        _server = new TableServer(HttpHost.Loopback, FreePort(), _registry, new TableFiles(Path.Combine(AppContext.BaseDirectory, "table")), admin, TimeProvider.System, _hosted.Stored, _accounts)
        {
            Practices = _practices,
            Scenarios = new ScenariosApi(scenarios, _practices, _accounts),
        };
        _serving = _server.RunAsync(_stopping.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        await Task.WhenAny(_serving);
        _server.Dispose();
        await _practices.DisposeAsync();
        _client.Dispose();
        _registry.Dispose();
        _hosted.Dispose();
        _stopping.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static int FreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? cookie, string? body = null, string? token = null)
    {
        using var request = new HttpRequestMessage(method, $"{_server.Url}{path.TrimStart('/')}");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (token is not null)
        {
            request.Headers.Add(TableApi.TokenHeader, token);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }

        return await _client.SendAsync(request, Cancel);
    }

    private async Task<string> GuestAsync()
    {
        var made = await SendAsync(HttpMethod.Post, AccountApi.GuestRoute, cookie: null, body: "{}");
        made.StatusCode.ShouldBe(HttpStatusCode.OK);
        return made.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }

    private async Task<string> SignedInAsync(string name)
    {
        var answer = await SendAsync(HttpMethod.Post, "/api/account/dev", cookie: null, body: JsonSerializer.Serialize(new { username = name }));
        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        return answer.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }

    private static async Task<string?> ErrorAsync(HttpResponseMessage answer) =>
        (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("error").GetString();

    [Theory]
    [InlineData("GET", "/api/lobby")]
    [InlineData("POST", "/api/lobby/tables")]
    [InlineData("GET", "/api/friends")]
    [InlineData("GET", "/api/leaderboard")]
    [InlineData("GET", "/api/stats")]
    [InlineData("POST", "/api/feedback")]
    [InlineData("GET", "/api/scenarios")]
    [InlineData("POST", "/api/scenarios/sc-repo-greedy-1v1-round-3/practice")]
    [InlineData("GET", "/api/account/identities")]
    [InlineData("POST", "/api/account/identities/google")]
    [InlineData("PUT", "/api/account/push")]
    [InlineData("GET", "/api/account/notices")]
    [InlineData("PUT", "/api/account/preferences/notices")]
    [InlineData("GET", "/api/tables")]
    [InlineData("GET", "/api/seat/player1")]
    public async Task A_guest_is_refused_every_route_it_is_not_listed_for(string method, string path)
    {
        var guest = await GuestAsync();

        var answer = await SendAsync(new HttpMethod(method), path, guest, method == "GET" ? null : "{}");

        answer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ErrorAsync(answer)).ShouldBe(AccountErrors.GuestNotAllowed.Code);
    }

    [Theory]
    [InlineData("GET", "/api/account")]
    [InlineData("GET", "/api/me")]
    [InlineData("GET", "/api/account/preferences")]
    [InlineData("GET", "/api/tutorials")]
    [InlineData("GET", "/api/account/names/MarkF")]
    [InlineData("GET", "/api/scenarios/" + Tutorial)]
    public async Task A_guest_reaches_its_account_its_settings_and_the_tutorials(string method, string path)
    {
        var guest = await GuestAsync();

        var answer = await SendAsync(new HttpMethod(method), path, guest);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK, await answer.Content.ReadAsStringAsync(Cancel));
    }

    [Fact]
    public async Task A_player_signed_in_is_not_held_to_the_guests_list()
    {
        var player = await SignedInAsync("MarkF");

        var answer = await SendAsync(HttpMethod.Get, "/api/friends", player);

        // This host keeps no friends: the route itself answers, not the guests' list.
        answer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_guest_opens_a_tutorial_and_plays_its_seat()
    {
        var guest = await GuestAsync();

        var opened = await SendAsync(HttpMethod.Post, $"/api/scenarios/{Tutorial}/practice", guest, "{}");
        opened.StatusCode.ShouldBe(HttpStatusCode.OK, await opened.Content.ReadAsStringAsync(Cancel));
        var token = (await opened.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("token").GetString();
        var seat = await SendAsync(HttpMethod.Get, "/api/seat/player1", guest, token: token);
        var feedback = await SendAsync(HttpMethod.Post, "/api/feedback", guest, "{}", token);

        seat.StatusCode.ShouldBe(HttpStatusCode.OK);
        feedback.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("GET", "/api/lobby")]
    [InlineData("POST", "/api/lobby/tables")]
    [InlineData("GET", "/api/friends")]
    [InlineData("PUT", "/api/account/push")]
    [InlineData("POST", "/api/account/notices/read")]
    [InlineData("GET", "/api/leaderboard")]
    [InlineData("GET", "/api/scenarios")]
    [InlineData("GET", "/api/tables")]
    public async Task A_guest_holding_its_tutorials_token_reaches_no_route_the_token_does_not_open(string method, string path)
    {
        var guest = await GuestAsync();
        var opened = await SendAsync(HttpMethod.Post, $"/api/scenarios/{Tutorial}/practice", guest, "{}");
        var token = (await opened.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("token").GetString();

        var answer = await SendAsync(new HttpMethod(method), path, guest, method == "GET" ? null : "{}", token);

        answer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ErrorAsync(answer)).ShouldBe(AccountErrors.GuestNotAllowed.Code);
    }

    [Fact]
    public async Task A_session_whose_account_cannot_be_read_is_asked_to_wait_rather_than_let_past_the_guests_list()
    {
        var guest = await GuestAsync();
        _clock.Advance(AccountApi.CacheFor);
        _store.Failing = true;

        var answer = await SendAsync(HttpMethod.Get, "/api/lobby", guest);

        answer.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await ErrorAsync(answer)).ShouldBe("Account.Unavailable");
    }

    [Fact]
    public async Task A_guest_does_not_reach_the_tutorial_of_another_guest()
    {
        var first = await GuestAsync();
        var second = await GuestAsync();
        var opened = await SendAsync(HttpMethod.Post, $"/api/scenarios/{Tutorial}/practice", first, "{}");
        var token = (await opened.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("token").GetString();

        var answer = await SendAsync(HttpMethod.Get, "/api/seat/player1", second, token: token);

        answer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ErrorAsync(answer)).ShouldBe(AccountErrors.GuestNotAllowed.Code);
    }

    /// <summary>The accounts on disk, whose reads of an account fail once <see cref="Failing" /> is set: the store out of service.</summary>
    private sealed class SwitchedStore(IAccountStore inner) : IAccountStore
    {
        public bool Failing { get; set; }

        public Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default) =>
            Failing ? throw new IOException("The account store is out of service (a test).") : inner.FindAsync(id, cancellationToken);

        public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default) => inner.ListAsync(cancellationToken);

        public Task<AccountRead?> ReadAsync(AccountId id, CancellationToken cancellationToken = default) =>
            Failing ? throw new IOException("The account store is out of service (a test).") : inner.ReadAsync(id, cancellationToken);

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

using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using DownfallArena.Cli.Hosting;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The routing ADR 0081 is about, over a real listener: a token reaches its own table and no other, the
/// lobby is behind its door, a close arrives as the page sends it, and the session page names one table.
/// </summary>
public sealed class TableServerTests : IAsyncDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry = new();
    private readonly Clock _clock = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly HttpClient _client = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
    private TableServer? _server;
    private Task? _serving;

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        if (_serving is not null)
        {
            await Task.WhenAny(_serving);
        }

        _server?.Dispose();
        _client.Dispose();
        _registry.Dispose();
        _hosted.Dispose();
        _stopping.Dispose();
    }

    [Fact]
    public async Task A_seat_token_reaches_its_own_table_s_seat_and_no_other()
    {
        var url = Serve(OperatorGate.BehindPlatform());
        var first = await Opened();
        var second = await Opened();

        var answer = await Get($"{url}api/seat/player1", second.Seats[0].Token);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var seat = await answer.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        seat.GetProperty("seat").GetString().ShouldBe("player1");
        seat.GetProperty("board").GetProperty("matchId").GetString().ShouldBe(second.Session.MatchId.ToString());
        seat.GetProperty("board").GetProperty("matchId").GetString().ShouldNotBe(first.Session.MatchId.ToString());
    }

    [Fact]
    public async Task An_ordinary_visit_opens_the_player_lobby_and_existing_seat_links_still_open_the_game()
    {
        var url = Serve(OperatorGate.BehindPlatform());
        var lobby = await Get(url, token: null);
        (await lobby.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("lobby.js");
        var game = await Get($"{url}?player1=invitation", token: null);
        (await game.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("table.js");
        (await Get($"{url}play", token: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Get($"{url}api/lobby", token: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Get($"{url}api/tables", token: null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>The stream of a table's changes (ADR 0119): what it says first, what it says on a change, and under whose token.</summary>
    [Fact]
    public async Task A_seat_streams_its_table_s_changes_as_they_happen()
    {
        var url = Serve(OperatorGate.BehindPlatform());
        // Two people: the first reaching their seat begins nothing, so the version moves only when this test moves it.
        var table = await Opened(HostedTables.TwoPeople);
        var token = table.Seats[0].Token;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{url}api/seat/player1/events");
        request.Headers.Add(TableApi.TokenHeader, token);

        using var answer = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        answer.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        using var reader = new StreamReader(await answer.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        (await Event(reader)).ShouldBe(("ready", table.Api.Changes.Version));
        var before = table.Api.Changes.Version;

        table.Api.Changes.Bump();

        (await Event(reader)).ShouldBe(("changed", before + 1));
        (await Get($"{url}api/seat/player1/events", table.Pilot.Token)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a pilot's token is not a seat's");
        (await Get($"{url}api/seat/player2/events", token)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, "the other seat's stream is not this token's");
    }

    /// <summary>What moves the version: a person reaching the seat, which begins the match and asks the first question; and the lobby's leave.</summary>
    [Fact]
    public async Task Reaching_a_seat_and_leaving_it_change_the_table()
    {
        var url = Serve(OperatorGate.BehindPlatform());
        var table = await Opened();
        var before = table.Api.Changes.Version;

        (await Get($"{url}api/seat/player1", table.Seats[0].Token)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(100, TestContext.Current.CancellationToken);
        table.Api.Changes.Version.ShouldBeGreaterThan(before, "the person arrived, the match began, and the seat was asked its first question");
    }

    /// <summary>One event off the stream: its name and the version it carries, past any keep-alive comment.</summary>
    private static async Task<(string Name, long Version)> Event(StreamReader reader)
    {
        string? name = null;
        long version = -1;
        var read = reader.ReadLineAsync(TestContext.Current.CancellationToken).AsTask();
        while (await read.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken) is { } line)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                name = line["event: ".Length..];
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                version = long.Parse(line["data: ".Length..], System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (line.Length == 0 && name is not null)
            {
                return (name, version);
            }

            read = reader.ReadLineAsync(TestContext.Current.CancellationToken).AsTask();
        }

        throw new InvalidOperationException("The stream ended before an event.");
    }

    [Fact]
    public async Task A_token_no_table_holds_is_refused_as_a_missing_one()
    {
        var url = Serve(OperatorGate.BehindPlatform());
        await Opened();

        (await Get($"{url}api/seat/player1", "nobody-s-token")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Get($"{url}api/session", token: null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_pilot_token_reaches_its_table_s_pilot_and_not_a_seat()
    {
        var url = Serve(OperatorGate.BehindPlatform());
        var table = await Opened();

        (await Get($"{url}api/pilot", table.Pilot.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Get($"{url}api/seat/player1", table.Pilot.Token)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Behind_the_platform_the_lobby_reads_the_stamped_principal_and_nothing_else()
    {
        var url = Serve(OperatorGate.BehindPlatform());

        (await Get($"{url}api/tables", token: null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}api/tables");
        request.Headers.Add(OperatorGate.PrincipalHeader, "mark@example.test");
        request.Content = JsonContent.Create(new { player1 = "person", player2 = "greedy" });
        var opened = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        opened.StatusCode.ShouldBe(HttpStatusCode.Created);
        _registry.All().Count.ShouldBe(1);
    }

    /// <summary>A close is a write, and the page sends it the way the same-origin fence takes a write.</summary>
    [Fact]
    public async Task A_close_sent_as_the_page_sends_it_takes_the_table_off_the_host()
    {
        var url = Serve(OperatorGate.WithToken("op-token"));
        var table = await Opened();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{url}api/tables/{table.Id}");
        request.Headers.Add(TableApi.TokenHeader, "op-token");
        request.Content = JsonContent.Create(new { });
        var closed = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        closed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _registry.ById(table.Id).ShouldBeNull();
        (await Get($"{url}api/seat/player1", table.Seats[0].Token)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_session_page_names_one_table_and_is_served_once_it_is_written()
    {
        var url = Serve(OperatorGate.BehindPlatform());
        var playing = await Opened();
        var finished = await Opened(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Finished(finished);

        (await Get($"{url}session/{finished.Id}", token: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Get($"{url}session/{playing.Id}", token: null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Get($"{url}session/nowhere", token: null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Reading a finished session counts as being at the table: the hour it is kept for runs from its last
    /// reader, so a session page someone keeps open is not swept from under them.
    /// </summary>
    [Fact]
    public async Task Reading_a_session_keeps_its_table_from_being_let_go_of()
    {
        var url = Serve(OperatorGate.BehindPlatform());
        var finished = await Opened(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Finished(finished);
        _clock.Now = finished.CreatedAt + TableRegistry.FinishedFor - TimeSpan.FromMinutes(10);
        (await Get($"{url}session/{finished.Id}", token: null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        _clock.Now = finished.CreatedAt + TableRegistry.FinishedFor + TimeSpan.FromMinutes(10);

        // Any request sweeps; this one is a stranger's, which is as good a sweep as any.
        (await Get($"{url}api/session", token: null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        _registry.ById(finished.Id).ShouldNotBeNull("it was read twenty minutes ago, not an hour ago");
        (await Get($"{url}session/{finished.Id}", token: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>A host whose lobby nobody opens still lets go: any request sweeps, by the host's clock.</summary>
    [Fact]
    public async Task Any_request_lets_go_of_a_table_nobody_has_been_at_for_long_enough()
    {
        var url = Serve(OperatorGate.BehindPlatform());
        var finished = await Opened(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Finished(finished);
        _clock.Now = finished.CreatedAt + TableRegistry.FinishedFor + TableRegistry.SweepEvery;

        (await Get($"{url}index.html", token: null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The page itself is served before the sweep; the next request finds the table gone.
        (await Get($"{url}api/session", token: null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        _registry.ById(finished.Id).ShouldBeNull();
    }

    /// <summary>The operator's page moved; a link printed before it did still arrives, token and all.</summary>
    [Fact]
    public async Task The_former_lobby_address_sends_the_operator_to_the_admin_panel_with_their_token()
    {
        var url = Serve(OperatorGate.WithToken("op-token"));

        var moved = await Get($"{url}lobby?token=op-token", token: null);

        moved.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        moved.Headers.Location.ShouldNotBeNull().ToString().ShouldBe("/admin?token=op-token");
        (await Get($"{url}admin", token: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A session whose table this process no longer has is read from the store: the replica that played it
    /// is gone, and the run is what survives (ADR 0080).
    /// </summary>
    [Fact]
    public async Task A_session_this_host_no_longer_holds_is_served_from_the_store()
    {
        var url = Serve(OperatorGate.BehindPlatform());
        var finished = await Opened(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Finished(finished);
        _registry.Remove(finished.Id).ShouldBeTrue();

        var page = await Get($"{url}session/{finished.Id}", token: null);

        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("manifest.json");
        (await Get($"{url}session/no-such-session", token: null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>On a host where players sign in, a seat answers its own account only, and the first to reach an unowned one signed in owns it (ADR 0122).</summary>
    [Fact]
    public async Task A_seat_answers_only_the_account_it_belongs_to()
    {
        var root = Path.Combine(Path.GetTempPath(), $"server-accounts-{Guid.CreateVersion7():N}");
        try
        {
            var url = await ServeWithAccountsAsync(root);
            var table = await Opened();
            var seat = $"{url}api/seat/player1";
            var mark = await SignInAsync(url, "MarkF");
            var bastien = await SignInAsync(url, "bastien");

            (await Get(seat, table.Seats[0].Token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await Get(seat, table.Seats[0].Token, mark)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await Get(seat, table.Seats[0].Token, bastien)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await Get(seat, table.Seats[0].Token, mark)).StatusCode.ShouldBe(HttpStatusCode.OK);
            table.Seats[0].Account.ShouldNotBeNull();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private async Task<string> ServeWithAccountsAsync(string root)
    {
        var store = new Infrastructure.Accounts.FileAccountStore(root);
        var accounts = new AccountApi(new Application.Accounts.AccountRegistry(store, _clock), new AccountSessions(await store.SessionKeyAsync(TestContext.Current.CancellationToken), _clock), AccountsMode.Development);
        var admin = new AdminApi(_registry, _hosted.Composer, OperatorGate.BehindPlatform(), _clock, "Rules 2 creatures (a test)", _hosted.Stored) { Accounts = accounts };
        _server = new TableServer(HttpHost.Loopback, FreePort(), _registry, new TableFiles(Path.Combine(AppContext.BaseDirectory, "table")), admin, _clock, _hosted.Stored, accounts);
        _serving = _server.RunAsync(_stopping.Token);
        return _server.Url;
    }

    /// <summary>Signs in by a typed name over the wire, and gives back the session cookie the host set.</summary>
    private async Task<string> SignInAsync(string url, string name)
    {
        using var content = new StringContent(JsonSerializer.Serialize(new { username = name }), System.Text.Encoding.UTF8, "application/json");
        var answer = await _client.PostAsync($"{url}api/account/dev", content, TestContext.Current.CancellationToken);
        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = answer.Headers.GetValues("Set-Cookie").Single();
        cookie.ShouldContain("HttpOnly");
        return cookie.Split(';')[0];
    }

    /// <summary>
    /// A host serves while it rebuilds the tables an earlier host left open, so the platform finds its port; a seat
    /// it does not hold yet is "try again", never "no such seat", which a page would read as a seat it lost.
    /// </summary>
    [Fact]
    public async Task While_the_tables_are_being_rebuilt_a_seat_not_back_yet_is_asked_to_wait_rather_than_refused()
    {
        var url = Serve(OperatorGate.BehindPlatform());
        _server!.Starting = true;

        var seat = await Get($"{url}api/seat/player1", "a-token-of-a-table-not-rebuilt-yet");
        using var resumeBody = new StringContent("""{"tokens":["a-token-of-a-table-not-rebuilt-yet"]}""", System.Text.Encoding.UTF8, "application/json");
        var resume = await _client.PostAsync($"{url}api/lobby/resume", resumeBody, TestContext.Current.CancellationToken);
        var listing = await Get($"{url}api/lobby", token: null);
        var page = await Get(url, token: null);

        seat.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        seat.Headers.RetryAfter.ShouldNotBeNull();
        resume.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        listing.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.StatusCode.ShouldBe(HttpStatusCode.OK);

        _server.Starting = false;
        (await Get($"{url}api/seat/player1", "a-token-of-a-table-not-rebuilt-yet")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private string Serve(OperatorGate gate)
    {
        var admin = new AdminApi(_registry, _hosted.Composer, gate, _clock, "Rules 2 creatures (a test)", _hosted.Stored);
        _server = new TableServer(HttpHost.Loopback, FreePort(), _registry, new TableFiles(Path.Combine(AppContext.BaseDirectory, "table")), admin, _clock, _hosted.Stored);
        _serving = _server.RunAsync(_stopping.Token);
        return _server.Url;
    }

    private async Task<PlayedTable> Opened(TableRequest? request = null)
    {
        var table = await _hosted.Composer.ComposeAsync(request ?? HostedTables.OnePerson, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();
        return table;
    }

    private Task<HttpResponseMessage> Get(string url, string? token, string? cookie = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null)
        {
            request.Headers.Add(TableApi.TokenHeader, token);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task Finished(PlayedTable table)
    {
        for (var attempt = 0; attempt < 300 && !table.IsFinished; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        table.IsFinished.ShouldBeTrue("the closing should have marked the table finished");
    }

    /// <summary>A port nothing else on this machine holds right now, asked of the machine rather than guessed.</summary>
    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    /// <summary>A clock a test moves, so a sweep is a fact rather than a wait.</summary>
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}

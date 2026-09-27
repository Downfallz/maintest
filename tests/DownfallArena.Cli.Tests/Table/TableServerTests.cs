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
    private readonly HttpClient _client = new();
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

    private string Serve(OperatorGate gate)
    {
        var lobby = new LobbyApi(_registry, _hosted.Composer, gate, _clock, "Rules 2 creatures (a test)");
        _server = new TableServer(HttpHost.Loopback, FreePort(), _registry, new TableFiles(Path.Combine(AppContext.BaseDirectory, "table")), lobby, _clock);
        _serving = _server.RunAsync(_stopping.Token);
        return _server.Url;
    }

    private async Task<PlayedTable> Opened(TableRequest? request = null)
    {
        var table = await _hosted.Composer.ComposeAsync(request ?? HostedTables.OnePerson, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();
        return table;
    }

    private Task<HttpResponseMessage> Get(string url, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null)
        {
            request.Headers.Add(TableApi.TokenHeader, token);
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

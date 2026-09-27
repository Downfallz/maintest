using System.Text;
using System.Text.Json;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The operator's routes: behind the door, and only the door. What these hold is who gets in, what a table
/// answers with when it is opened, and that the listing says where each one is.
/// </summary>
public sealed class LobbyApiTests : IDisposable
{
    private const string OperatorToken = "token-of-the-operator";

    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry = new(mostUnderWay: 1);

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
    }

    [Fact]
    public async Task Without_the_operator_s_token_the_lobby_answers_nothing_but_the_refusal()
    {
        var lobby = Lobby(OperatorGate.WithToken(OperatorToken));

        (await lobby.HandleAsync("GET", "/api/tables", "", token: null, principal: null)).Status.ShouldBe(403);
        (await lobby.HandleAsync("GET", "/api/tables", "", "a-seat-s-token", principal: null)).Status.ShouldBe(403);
        (await lobby.HandleAsync("POST", "/api/tables", "{}", "a-seat-s-token", principal: null)).Status.ShouldBe(403);

        // The platform's stamp means nothing to a host that was not told to trust it.
        (await lobby.HandleAsync("GET", "/api/tables", "", token: null, principal: "mark@example.test")).Status.ShouldBe(403);
    }

    [Fact]
    public async Task Behind_the_platform_a_request_nobody_signed_is_told_where_to_sign_in()
    {
        var lobby = Lobby(OperatorGate.BehindPlatform());

        var answer = await lobby.HandleAsync("GET", "/api/tables", "", token: null, principal: null);

        answer.Status.ShouldBe(401);
        var body = JsonDocument.Parse(Text(answer)).RootElement;
        body.GetProperty("error").GetString().ShouldBe("Lobby.SignIn");
        body.GetProperty("login").GetString().ShouldBe("/.auth/login/aad?post_login_redirect_uri=/lobby");

        (await lobby.HandleAsync("GET", "/api/tables", "", token: null, principal: "mark@example.test")).Status.ShouldBe(200);
    }

    [Fact]
    public async Task Opening_a_table_answers_with_each_seat_s_code_the_pilot_and_where_it_is_written()
    {
        var lobby = Lobby(OperatorGate.WithToken(OperatorToken));

        var answer = await lobby.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy","who":"mk"}""", OperatorToken, null);

        answer.Status.ShouldBe(201, Text(answer));
        var opened = JsonDocument.Parse(Text(answer)).RootElement;
        var id = opened.GetProperty("id").GetString().ShouldNotBeNull();
        opened.GetProperty("over").GetBoolean().ShouldBeFalse();
        opened.GetProperty("location").GetString().ShouldNotBeNull().ShouldEndWith(id);
        opened.GetProperty("pilot").GetString().ShouldNotBeNull().ShouldStartWith("/pilot?token=");
        opened.GetProperty("session").GetString().ShouldBe($"/session/{id}");
        opened.GetProperty("hotseat").ValueKind.ShouldBe(JsonValueKind.Null, "one person is one browser");

        var seats = opened.GetProperty("seats").EnumerateArray().ToList();
        seats[0].GetProperty("slot").GetString().ShouldBe("player1");
        seats[0].GetProperty("seated").GetString().ShouldBe("human:mk");
        seats[0].GetProperty("hasPerson").GetBoolean().ShouldBeTrue();
        seats[0].GetProperty("code").GetString().ShouldNotBeNull().Length.ShouldBe(8);
        seats[0].GetProperty("join").GetString().ShouldStartWith("/j/");
        seats[0].GetProperty("link").GetString().ShouldStartWith("/?player1=");
        seats[1].GetProperty("seated").GetString().ShouldBe("Greedy");
        seats[1].GetProperty("code").ValueKind.ShouldBe(JsonValueKind.Null);

        // And the table is now the registry's, found by the token the link carries.
        var token = seats[0].GetProperty("link").GetString()!["/?player1=".Length..];
        _registry.ByToken(token).ShouldNotBeNull().Id.ShouldBe(id);
    }

    [Fact]
    public async Task Two_people_get_two_codes_and_one_link_for_one_browser()
    {
        var lobby = Lobby(OperatorGate.WithToken(OperatorToken));

        var answer = await lobby.HandleAsync("POST", "/api/tables", "{}", OperatorToken, null);

        answer.Status.ShouldBe(201, Text(answer));
        var opened = JsonDocument.Parse(Text(answer)).RootElement;
        var codes = opened.GetProperty("seats").EnumerateArray().Select(seat => seat.GetProperty("code").GetString()).ToList();
        codes.ShouldAllBe(code => code != null && code.Length == 8);
        codes[0].ShouldNotBe(codes[1]);
        opened.GetProperty("hotseat").GetString().ShouldNotBeNull().ShouldContain("player1=");
    }

    [Fact]
    public async Task The_listing_says_where_every_table_is()
    {
        var lobby = Lobby(OperatorGate.WithToken(OperatorToken));
        (await lobby.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy"}""", OperatorToken, null)).Status.ShouldBe(201);

        var answer = await lobby.HandleAsync("GET", "/api/tables", "", OperatorToken, null);

        answer.Status.ShouldBe(200, Text(answer));
        var listed = JsonDocument.Parse(Text(answer)).RootElement;
        listed.GetProperty("recording").GetBoolean().ShouldBeTrue();
        listed.GetProperty("rules").GetString().ShouldNotBeNull().ShouldStartWith("Rules ");
        var tables = listed.GetProperty("tables").EnumerateArray().ToList();
        tables.Count.ShouldBe(1);
        tables[0].GetProperty("finished").GetBoolean().ShouldBeFalse();
        tables[0].GetProperty("seats").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task A_name_nothing_can_be_built_from_is_the_operator_s_typo()
    {
        var lobby = Lobby(OperatorGate.WithToken(OperatorToken));

        (await lobby.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"clairvoyant:nowhere.json"}""", OperatorToken, null)).Status.ShouldBe(400);
        (await lobby.HandleAsync("POST", "/api/tables", """{"player1":"person","handover":0}""", OperatorToken, null)).Status.ShouldBe(400);
        (await lobby.HandleAsync("POST", "/api/tables", "not json", OperatorToken, null)).Status.ShouldBe(400);
        _registry.All().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_host_with_as_many_tables_under_way_as_it_takes_refuses_another_by_name()
    {
        var lobby = Lobby(OperatorGate.WithToken(OperatorToken));
        (await lobby.HandleAsync("POST", "/api/tables", "{}", OperatorToken, null)).Status.ShouldBe(201);

        var answer = await lobby.HandleAsync("POST", "/api/tables", "{}", OperatorToken, null);

        answer.Status.ShouldBe(409, Text(answer));
        JsonDocument.Parse(Text(answer)).RootElement.GetProperty("error").GetString().ShouldBe("Lobby.Full");
    }

    [Fact]
    public async Task Closing_a_table_takes_it_off_the_list_and_stops_its_match()
    {
        var lobby = Lobby(OperatorGate.WithToken(OperatorToken));
        var opened = JsonDocument.Parse(Text(await lobby.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy"}""", OperatorToken, null))).RootElement;
        var id = opened.GetProperty("id").GetString()!;
        var table = _registry.ById(id).ShouldNotBeNull();

        (await lobby.HandleAsync("DELETE", $"/api/tables/{id}", "", OperatorToken, null)).Status.ShouldBe(204);

        (await lobby.HandleAsync("DELETE", $"/api/tables/{id}", "", OperatorToken, null)).Status.ShouldBe(404);
        _registry.All().ShouldBeEmpty();
        await Should.ThrowAsync<OperationCanceledException>(() => table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_lobby_knows_its_own_routes()
    {
        LobbyApi.Names("/api/tables").ShouldBeTrue();
        LobbyApi.Names("/api/tables/x").ShouldBeTrue();
        LobbyApi.Names("/api/tablesmith").ShouldBeFalse();
        LobbyApi.Names("/api/seat/player1").ShouldBeFalse();

        var lobby = Lobby(OperatorGate.WithToken(OperatorToken));
        (await lobby.HandleAsync("PUT", "/api/tables", "", OperatorToken, null)).Status.ShouldBe(404);
    }

    private LobbyApi Lobby(OperatorGate gate) => new(_registry, _hosted.Composer, gate, TimeProvider.System, "Rules 2 creatures (a test)");

    private static string Text(StudioResponse response) => Encoding.UTF8.GetString(response.Body);
}

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The operator's routes: behind the door, and only the door. What these hold is who gets in, what a table
/// answers with when it is opened, and that the listing says where each one is.
/// </summary>
public sealed class AdminApiTests : IDisposable
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
    public async Task Without_the_operator_s_token_the_panel_answers_nothing_but_the_refusal()
    {
        var admin = Admin(OperatorGate.WithToken(OperatorToken));

        (await admin.HandleAsync("GET", "/api/tables", "", token: null, principal: null)).Status.ShouldBe(403);
        (await admin.HandleAsync("GET", "/api/tables", "", "a-seat-s-token", principal: null)).Status.ShouldBe(403);
        (await admin.HandleAsync("POST", "/api/tables", "{}", "a-seat-s-token", principal: null)).Status.ShouldBe(403);

        // The platform's stamp means nothing to a host that was not told to trust it.
        (await admin.HandleAsync("GET", "/api/tables", "", token: null, principal: "mark@example.test")).Status.ShouldBe(403);
    }

    [Fact]
    public async Task Behind_the_platform_a_request_nobody_signed_is_told_where_to_sign_in()
    {
        var admin = Admin(OperatorGate.BehindPlatform());

        var answer = await admin.HandleAsync("GET", "/api/tables", "", token: null, principal: null);

        answer.Status.ShouldBe(401);
        var body = JsonDocument.Parse(Text(answer)).RootElement;
        body.GetProperty("error").GetString().ShouldBe("Admin.SignIn");
        body.GetProperty("login").GetString().ShouldBe("/.auth/login/aad?post_login_redirect_uri=/admin");

        (await admin.HandleAsync("GET", "/api/tables", "", token: null, principal: "mark@example.test")).Status.ShouldBe(200);
    }

    [Fact]
    public async Task Opening_a_table_answers_with_each_seat_s_code_the_pilot_and_where_it_is_written()
    {
        var admin = Admin(OperatorGate.WithToken(OperatorToken));

        var answer = await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy","who":"mk"}""", OperatorToken, null);

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
        var admin = Admin(OperatorGate.WithToken(OperatorToken));

        var answer = await admin.HandleAsync("POST", "/api/tables", "{}", OperatorToken, null);

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
        var admin = Admin(OperatorGate.WithToken(OperatorToken));
        (await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy"}""", OperatorToken, null)).Status.ShouldBe(201);

        var answer = await admin.HandleAsync("GET", "/api/tables", "", OperatorToken, null);

        answer.Status.ShouldBe(200, Text(answer));
        var listed = JsonDocument.Parse(Text(answer)).RootElement;
        listed.GetProperty("recording").GetBoolean().ShouldBeTrue();
        listed.GetProperty("rules").GetString().ShouldNotBeNull().ShouldStartWith("Rules ");
        var tables = listed.GetProperty("tables").EnumerateArray().ToList();
        tables.Count.ShouldBe(1);
        tables[0].GetProperty("finished").GetBoolean().ShouldBeFalse();
        tables[0].GetProperty("seats").GetArrayLength().ShouldBe(2);
    }

    [Theory]
    [InlineData("""{"player1":"person","player2":"clairvoyant:nowhere.json"}""")]
    [InlineData("""{"player1":"person","handover":0}""")]
    [InlineData("not json")]
    public async Task A_request_that_is_not_a_table_is_refused_before_anything_is_composed(string body)
    {
        var admin = Admin(OperatorGate.WithToken(OperatorToken));

        (await admin.HandleAsync("POST", "/api/tables", body, OperatorToken, null)).Status.ShouldBe(400);

        _registry.All().ShouldBeEmpty();
    }

    /// <summary>A spec that parses but names a file that is not there fails while composing, and is the operator's typo too.</summary>
    [Fact]
    public async Task A_weights_file_that_is_not_there_is_the_operator_s_typo_not_a_broken_host()
    {
        var admin = Admin(OperatorGate.WithToken(OperatorToken));

        var answer = await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"heuristic:nowhere.json"}""", OperatorToken, null);

        answer.Status.ShouldBe(400, Text(answer));
        _registry.All().ShouldBeEmpty();
    }

    /// <summary>
    /// Refused before it is composed: a table composed and then refused would have opened a recording and
    /// written a run nobody played.
    /// </summary>
    [Fact]
    public async Task A_host_with_as_many_tables_under_way_as_it_takes_refuses_another_by_name_and_writes_nothing()
    {
        var admin = Admin(OperatorGate.WithToken(OperatorToken));
        (await admin.HandleAsync("POST", "/api/tables", "{}", OperatorToken, null)).Status.ShouldBe(201);

        var answer = await admin.HandleAsync("POST", "/api/tables", "{}", OperatorToken, null);

        answer.Status.ShouldBe(409, Text(answer));
        JsonDocument.Parse(Text(answer)).RootElement.GetProperty("error").GetString().ShouldBe("Admin.Full");
        Directory.GetDirectories(_hosted.RunsDirectory).Length.ShouldBe(1, "the refused table must not have opened a run");
    }

    [Fact]
    public async Task Closing_a_table_takes_it_off_the_list_and_stops_its_match()
    {
        var admin = Admin(OperatorGate.WithToken(OperatorToken));
        var opened = JsonDocument.Parse(Text(await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy"}""", OperatorToken, null))).RootElement;
        var id = opened.GetProperty("id").GetString()!;
        var table = _registry.ById(id).ShouldNotBeNull();

        (await admin.HandleAsync("DELETE", $"/api/tables/{id}", "", OperatorToken, null)).Status.ShouldBe(204);

        (await admin.HandleAsync("DELETE", $"/api/tables/{id}", "", OperatorToken, null)).Status.ShouldBe(404);
        _registry.All().ShouldBeEmpty();
        await Should.ThrowAsync<OperationCanceledException>(() => table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_panel_knows_its_own_routes()
    {
        AdminApi.Names("/api/tables").ShouldBeTrue();
        AdminApi.Names("/api/tables/x").ShouldBeTrue();
        AdminApi.Names("/api/tablesmith").ShouldBeFalse();
        AdminApi.Names("/api/seat/player1").ShouldBeFalse();
        AdminApi.Names("/api/sessions").ShouldBeTrue();
        AdminApi.Names("/api/sessions/x/export").ShouldBeTrue();
        AdminApi.Names("/api/me").ShouldBeTrue();

        var admin = Admin(OperatorGate.WithToken(OperatorToken));
        (await admin.HandleAsync("PUT", "/api/tables", "", OperatorToken, null)).Status.ShouldBe(404);
    }

    [Fact]
    public async Task The_listing_offers_the_bots_the_host_was_given_in_their_order()
    {
        var admin = Admin(OperatorGate.WithToken(OperatorToken), agents: [new SeatableAgent("heuristic:w/search-19.json", "search-19", Featured: true), new SeatableAgent("greedy", "Greedy", Featured: false)]);

        var listed = JsonDocument.Parse(Text(await admin.HandleAsync("GET", "/api/tables", "", OperatorToken, null))).RootElement;

        var agents = listed.GetProperty("agents").EnumerateArray().ToList();
        agents.Select(agent => agent.GetProperty("value").GetString()).ShouldBe(["heuristic:w/search-19.json", "greedy"]);
        agents[0].GetProperty("label").GetString().ShouldBe("search-19");
        agents[0].GetProperty("featured").GetBoolean().ShouldBeTrue();
        agents[1].GetProperty("featured").GetBoolean().ShouldBeFalse();
    }

    /// <summary>Open to anybody, and says nothing but yes or no: the table page asks it to show the way back to the panel.</summary>
    [Fact]
    public async Task Whether_a_request_is_the_operator_s_is_answered_to_anybody_without_a_refusal()
    {
        var admin = Admin(OperatorGate.WithToken(OperatorToken));

        var mine = JsonDocument.Parse(Text(await admin.HandleAsync("GET", "/api/me", "", OperatorToken, null))).RootElement;
        var theirs = await admin.HandleAsync("GET", "/api/me", "", "a-seat-s-token", null);

        mine.GetProperty("operator").GetBoolean().ShouldBeTrue();
        mine.GetProperty("admin").GetString().ShouldBe("/admin");
        theirs.Status.ShouldBe(200);
        JsonDocument.Parse(Text(theirs)).RootElement.GetProperty("operator").GetBoolean().ShouldBeFalse();

        var platform = Admin(OperatorGate.BehindPlatform());
        JsonDocument.Parse(Text(await platform.HandleAsync("GET", "/api/me", "", null, "mark@example.test"))).RootElement.GetProperty("operator").GetBoolean().ShouldBeTrue();
        JsonDocument.Parse(Text(await platform.HandleAsync("GET", "/api/me", "", null, null))).RootElement.GetProperty("operator").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task The_store_s_sessions_are_listed_with_what_their_manifest_says_and_whether_this_host_still_has_them()
    {
        var admin = Admin(OperatorGate.WithToken(OperatorToken));
        var live = Opened(await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy","who":"mk"}""", OperatorToken, null));
        _registry.Remove(live);
        var gone = Opened(await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy","who":"mk"}""", OperatorToken, null));
        _registry.Remove(gone);
        var kept = Opened(await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy","who":"mk"}""", OperatorToken, null));

        var answer = await admin.HandleAsync("GET", "/api/sessions", "", OperatorToken, null);

        answer.Status.ShouldBe(200, Text(answer));
        var sessions = JsonDocument.Parse(Text(answer)).RootElement.GetProperty("sessions").EnumerateArray().ToList();
        sessions.Select(session => session.GetProperty("id").GetString()).ShouldBe(new[] { kept, gone, live }.OrderByDescending(id => id, StringComparer.Ordinal), "newest first");
        var still = sessions.Single(session => session.GetProperty("id").GetString() == kept);
        still.GetProperty("live").GetBoolean().ShouldBeTrue();
        still.GetProperty("player1").GetString().ShouldBe("human:mk");
        still.GetProperty("player2").GetString().ShouldBe("Greedy");
        still.GetProperty("session").GetString().ShouldBe($"/session/{kept}");
        still.GetProperty("export").GetString().ShouldBe($"/api/sessions/{kept}/export");
        still.GetProperty("location").GetString().ShouldNotBeNull().ShouldEndWith(kept);
        sessions.Single(session => session.GetProperty("id").GetString() == gone).GetProperty("live").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Deleting_sessions_closes_a_table_still_being_played_and_removes_the_runs_named()
    {
        var admin = Admin(OperatorGate.WithToken(OperatorToken));
        var playing = Opened(await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy"}""", OperatorToken, null));
        var table = _registry.ById(playing).ShouldNotBeNull();

        var answer = await admin.HandleAsync("DELETE", "/api/sessions", $$"""{"ids":["{{playing}}","nowhere"]}""", OperatorToken, null);

        answer.Status.ShouldBe(200, Text(answer));
        var told = JsonDocument.Parse(Text(answer)).RootElement;
        told.GetProperty("deleted").EnumerateArray().Select(id => id.GetString()).ShouldBe([playing]);
        told.GetProperty("missing").EnumerateArray().Select(id => id.GetString()).ShouldBe(["nowhere"]);
        _registry.ById(playing).ShouldBeNull();
        Directory.Exists(Path.Combine(_hosted.RunsDirectory, playing)).ShouldBeFalse("the run goes with the table");
        await Should.ThrowAsync<OperationCanceledException>(() => table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        (await admin.HandleAsync("DELETE", "/api/sessions", """{"ids":[]}""", OperatorToken, null)).Status.ShouldBe(400);
        (await admin.HandleAsync("DELETE", "/api/sessions", "not json", OperatorToken, null)).Status.ShouldBe(400);
    }

    [Fact]
    public async Task An_export_is_a_zip_of_run_directories_as_the_store_holds_them()
    {
        var admin = Admin(OperatorGate.WithToken(OperatorToken));
        var first = Opened(await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy"}""", OperatorToken, null));
        _registry.Remove(first);
        var second = Opened(await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"greedy"}""", OperatorToken, null));

        var one = await admin.HandleAsync("GET", $"/api/sessions/{first}/export", "", OperatorToken, null);
        var both = await admin.HandleAsync("POST", "/api/sessions/export", $$"""{"ids":["{{first}}","{{second}}","nowhere"]}""", OperatorToken, null);

        one.Status.ShouldBe(200, Text(one));
        one.ContentType.ShouldBe("application/zip");
        one.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Content-Disposition" && header.Value.Contains($"{first}.zip"));
        using (var zip = new ZipArchive(new MemoryStream(one.Body), ZipArchiveMode.Read))
        {
            zip.Entries.Select(entry => entry.FullName).ShouldContain($"{first}/manifest.json");
            zip.Entries.Select(entry => entry.FullName).ShouldContain($"{first}/catalogue.json");
            zip.Entries.Select(entry => entry.FullName).ShouldContain(name => name.StartsWith($"{first}/traces/", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal));
            using var reader = new StreamReader(zip.GetEntry($"{first}/manifest.json")!.Open());
            JsonDocument.Parse(await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).RootElement.GetProperty("stamp").GetProperty("player2Agent").GetString().ShouldBe("Greedy");
        }

        both.Status.ShouldBe(200, Text(both));
        using (var zip = new ZipArchive(new MemoryStream(both.Body), ZipArchiveMode.Read))
        {
            zip.Entries.Select(entry => entry.FullName).ShouldContain($"{first}/manifest.json");
            zip.Entries.Select(entry => entry.FullName).ShouldContain($"{second}/manifest.json");
            zip.Entries.ShouldAllBe(entry => !entry.FullName.StartsWith("nowhere/", StringComparison.Ordinal));
        }

        (await admin.HandleAsync("GET", "/api/sessions/nowhere/export", "", OperatorToken, null)).Status.ShouldBe(404);
    }

    [Fact]
    public async Task A_host_that_records_nothing_has_no_session_to_list_and_says_so()
    {
        using var silent = new HostedTables(recording: false);
        var admin = new AdminApi(_registry, silent.Composer, OperatorGate.WithToken(OperatorToken), TimeProvider.System, "Rules 2 creatures (a test)");

        var answer = await admin.HandleAsync("GET", "/api/sessions", "", OperatorToken, null);

        answer.Status.ShouldBe(404);
        JsonDocument.Parse(Text(answer)).RootElement.GetProperty("error").GetString().ShouldBe("Admin.NotRecording");
    }

    private static string Opened(StudioResponse answer)
    {
        answer.Status.ShouldBe(201, Text(answer));
        return JsonDocument.Parse(Text(answer)).RootElement.GetProperty("id").GetString()!;
    }

    private AdminApi Admin(OperatorGate gate, IReadOnlyList<SeatableAgent>? agents = null) =>
        new(_registry, _hosted.Composer, gate, TimeProvider.System, "Rules 2 creatures (a test)", _hosted.Stored, agents);

    private static string Text(StudioResponse response) => Encoding.UTF8.GetString(response.Body);
}

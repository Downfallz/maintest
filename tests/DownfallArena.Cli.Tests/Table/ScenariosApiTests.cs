using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Admin;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.Admin;
using DownfallArena.Infrastructure.Learning;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// Match scenarios at the hosted table (docs/tabletop/scenarios-plan.md, S4, ADR 0179): the operator cuts one from a
/// recorded session, and a signed-in player practises it in this process's memory, one practice an account, nothing
/// of it recorded, indexed or written back to the session.
/// </summary>
public sealed class ScenariosApiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"scenarios-{Guid.CreateVersion7():N}");
    private readonly HostedTables _hosted = new(recording: true);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 14, 4, 0, 0, TimeSpan.Zero));
    private readonly FileAccountStore _accounts;
    private readonly ScenarioStore _store;

    public ScenariosApiTests()
    {
        _accounts = new FileAccountStore(_root);
        _store = new ScenarioStore(new FileArtifactStore(Path.Combine(_root, "scenarios")), _clock);
    }

    public void Dispose()
    {
        _hosted.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private ScenarioAdminApi Admin() => new(_store, _hosted.Stored!, _hosted.Composer);

    private async Task<AccountApi> AccountsAsync() =>
        new(new AccountRegistry(_accounts, TimeProvider.System), new AccountSessions(await _accounts.SessionKeyAsync(Cancel), TimeProvider.System), AccountsMode.Development);

    private static async Task<AccountRequest> SignedInAsync(AccountApi accounts, string name)
    {
        var signedIn = await accounts.HandleAsync("POST", "/api/account/dev", JsonSerializer.Serialize(new { username = name }), new AccountRequest(null, Secure: false));
        return new AccountRequest(signedIn.Headers!.Single().Value.Split(';')[0][(AccountSessions.Cookie.Length + 1)..], Secure: false);
    }

    /// <summary>A table of two bots played to its end and recorded, then cut at its second round by the operator.</summary>
    private async Task<(string Session, string Scenario)> CutAsync()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.Bots, Cancel);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), Cancel);
        for (var waited = 0; !table.IsFinished && waited < 300; waited++)
        {
            await Task.Delay(100, Cancel);
        }

        await table.RecordAsync(Cancel);
        var cut = await Admin().HandleAsync("POST", ScenarioAdminApi.Route, JsonSerializer.Serialize(new { session = table.Id, round = 2, seat = "player2", title = "Second round" }), "operator");
        cut.Status.ShouldBe(201, Encoding.UTF8.GetString(cut.Body));
        return (table.Id, Read(cut).GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task The_operator_cuts_a_recorded_session_at_a_round_and_finds_it_listed_with_its_link()
    {
        var (session, scenario) = await CutAsync();

        var listed = Read(await Admin().HandleAsync("GET", ScenarioAdminApi.Route, "", "operator")).GetProperty("scenarios")[0];

        listed.GetProperty("id").GetString().ShouldBe(scenario);
        listed.GetProperty("session").GetString().ShouldBe(session);
        listed.GetProperty("round").GetInt32().ShouldBe(2);
        listed.GetProperty("seat").GetString().ShouldBe("player2");
        listed.GetProperty("link").GetString().ShouldBe($"/battle?scenario={scenario}");
    }

    [Fact]
    public async Task A_scenario_cut_and_removed_is_noted_in_the_operators_activity()
    {
        var (session, first) = await CutAsync();
        // The test's clock stands still: the cut and the removal are noted at one instant, and read in the order noted (#674).
        var actions = new OperatorActions(new FileOperatorLog(_root), _clock);
        var admin = new ScenarioAdminApi(_store, _hosted.Stored!, _hosted.Composer) { Notes = new OperatorNotes(actions) };

        var cut = await admin.HandleAsync("POST", ScenarioAdminApi.Route, JsonSerializer.Serialize(new { session, round = 2 }), "mark");
        var removed = await admin.HandleAsync("DELETE", $"{ScenarioAdminApi.Route}/{first}", "", "mark");

        (cut.Status, removed.Status).ShouldBe((201, 204));
        var noted = (await actions.PageAsync(1, Cancel)).Actions.Select(one => (one.Action, one.By, one.Target)).ToList();
        noted.ShouldBe([(OperatorActions.RemoveScenario, "mark", first), (OperatorActions.CutScenario, "mark", Read(cut).GetProperty("id").GetString())]);
    }

    [Fact]
    public async Task A_round_the_session_does_not_offer_is_refused_with_the_rounds_it_does()
    {
        var (session, _) = await CutAsync();

        var refused = await Admin().HandleAsync("POST", ScenarioAdminApi.Route, JsonSerializer.Serialize(new { session, round = 99 }), "operator");

        refused.Status.ShouldBe(409);
        Read(refused).GetProperty("rounds").EnumerateArray().Select(one => one.GetInt32()).ShouldContain(2);
    }

    [Fact]
    public async Task The_operator_reads_the_rounds_a_session_can_be_cut_at_before_choosing_one()
    {
        var (session, _) = await CutAsync();

        var rounds = Read(await Admin().HandleAsync("GET", $"{ScenarioAdminApi.Route}/rounds", "", "operator", $"?session={session}"));

        rounds.GetProperty("rounds").EnumerateArray().Select(one => one.GetInt32()).ShouldBe(Enumerable.Range(1, rounds.GetProperty("rounds").GetArrayLength()));
        rounds.GetProperty("stoppedBy").ValueKind.ShouldBe(JsonValueKind.Null);
        (await Admin().HandleAsync("GET", $"{ScenarioAdminApi.Route}/rounds", "", "operator", "?session=20261114-000000-deadbeef")).Status.ShouldBe(404);
    }

    /// <summary>
    /// A table still played is not cut (#736): its journal holds the other seat's choices of the round, face down, which
    /// the scripted opponent would follow and any signed-in account holding the scenario's id would open.
    /// </summary>
    [Fact]
    public async Task A_session_still_under_way_is_neither_cut_nor_read_for_its_rounds_until_it_is_over()
    {
        using var registry = _hosted.Registry();
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        var seat = table.Seats[0];
        (await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token)).Status.ShouldBe(200);

        var cut = await Admin().HandleAsync("POST", ScenarioAdminApi.Route, JsonSerializer.Serialize(new { session = table.Id, round = 1 }), "operator");
        var rounds = await Admin().HandleAsync("GET", $"{ScenarioAdminApi.Route}/rounds", "", "operator", $"?session={table.Id}");

        (cut.Status, Read(cut).GetProperty("error").GetString()).ShouldBe((409, "Scenario.UnderWay"));
        (rounds.Status, Read(rounds).GetProperty("error").GetString()).ShouldBe((409, "Scenario.UnderWay"));
        (await _store.ListAsync(Cancel)).ShouldBeEmpty();

        // Closed by the operator, it is over: the same round is cut.
        (await _hosted.Stored!.CloseAsync(table.Id, _hosted.Index, Cancel)).ShouldBeTrue();
        var later = await Admin().HandleAsync("POST", ScenarioAdminApi.Route, JsonSerializer.Serialize(new { session = table.Id, round = 1 }), "operator");
        later.Status.ShouldBe(201, Encoding.UTF8.GetString(later.Body));
    }

    /// <summary>
    /// A scenario cut before #736 from a table still played holds the other seat's face-down choices of its round
    /// (#833): until that game is over it is neither opened nor resumed by id, listed under Practice or offered there.
    /// </summary>
    [Fact]
    public async Task A_scenario_cut_from_a_game_still_played_is_neither_opened_listed_nor_offered_until_it_is_over()
    {
        var (session, scenario) = await CutAsync();
        (await Admin().HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{scenario}/listing", """{"listed":true}""", "operator")).Status.ShouldBe(200);
        await StillPlayed.ReopenAsync(_hosted.RunsDirectory, session);
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts) { Stored = _hosted.Stored };
        var learner = await SignedInAsync(accounts, "learner");

        var opened = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", learner);
        var resumed = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{scenario}/practice", learner);
        var listing = await Admin().HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{scenario}/listing", """{"listed":true}""", "operator");
        var offered = Read(await api.HandleAsync("GET", ScenariosApi.Prefix, learner)).GetProperty("scenarios").GetArrayLength();
        var takenOff = await Admin().HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{scenario}/listing", """{"listed":false}""", "operator");

        (opened.Status, Read(opened).GetProperty("error").GetString()).ShouldBe((409, "Scenario.UnderWay"));
        (resumed.Status, Read(resumed).GetProperty("error").GetString()).ShouldBe((409, "Scenario.UnderWay"));
        (listing.Status, Read(listing).GetProperty("error").GetString()).ShouldBe((409, "Scenario.UnderWay"));
        offered.ShouldBe(0);
        takenOff.Status.ShouldBe(200, "taking it off is always allowed");
    }

    /// <summary>Once the game it was cut from is over, the same scenario opens again (#833).</summary>
    [Fact]
    public async Task A_scenario_cut_from_a_game_since_over_opens()
    {
        var (session, scenario) = await CutAsync();
        await StillPlayed.ReopenAsync(_hosted.RunsDirectory, session);
        (await _hosted.Stored!.CloseAsync(session, _hosted.Index, Cancel)).ShouldBeTrue();
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts) { Stored = _hosted.Stored };

        var opened = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", await SignedInAsync(accounts, "learner"));

        opened.Status.ShouldBe(200, Encoding.UTF8.GetString(opened.Body));
    }

    /// <summary>A scenario whose source record cannot be parsed is held back too: nothing shows that game is over (#833, #814).</summary>
    [Fact]
    public async Task A_scenario_whose_source_record_cannot_be_read_is_not_opened()
    {
        var (session, scenario) = await CutAsync();
        await File.WriteAllTextAsync(Path.Combine(_hosted.RunsDirectory, session, TableRecord.File), "not a record", Cancel);
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts) { Stored = _hosted.Stored };

        var opened = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", await SignedInAsync(accounts, "learner"));

        (opened.Status, Read(opened).GetProperty("error").GetString()).ShouldBe((409, "Scenario.SourceUnread"));
    }

    /// <summary>
    /// A store that cannot say whether the source game is over fails no list (#833): the scenario is left off Practice,
    /// said in one console line, and opening it answers 503 rather than an unexpected failure.
    /// </summary>
    [Fact]
    public async Task A_store_that_cannot_say_whether_the_source_game_is_over_leaves_the_scenario_off_and_answers_busy()
    {
        var (_, scenario) = await CutAsync();
        (await Admin().HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{scenario}/listing", """{"listed":true}""", "operator")).Status.ShouldBe(200);
        var unreadable = new StoredSessions(new RecordRefused(new FileArtifactStore(_hosted.RunsDirectory)) { ReadsRefused = true });
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var said = new List<string>();
        var api = new ScenariosApi(_store, practices, accounts) { Stored = unreadable, Said = said.Add };
        var learner = await SignedInAsync(accounts, "learner");

        var listed = await api.HandleAsync("GET", ScenariosApi.Prefix, learner);
        var opened = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", learner);
        var listing = await new ScenarioAdminApi(_store, unreadable, _hosted.Composer) { Said = said.Add }.HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{scenario}/listing", """{"listed":true}""", "operator");

        (listed.Status, Read(listed).GetProperty("scenarios").GetArrayLength()).ShouldBe((200, 0));
        (opened.Status, Read(opened).GetProperty("error").GetString()).ShouldBe((503, "Scenario.Unavailable"));
        (listing.Status, Read(listing).GetProperty("error").GetString()).ShouldBe((503, "Scenario.Unavailable"));
        said.Count.ShouldBe(3);
        said[0].ShouldStartWith("  1 listed scenario was left off Practice");
    }

    [Fact]
    public async Task A_session_the_host_never_recorded_cannot_be_cut() =>
        (await Admin().HandleAsync("POST", ScenarioAdminApi.Route, JsonSerializer.Serialize(new { session = "20261114-000000-deadbeef", round = 1 }), "operator")).Status.ShouldBe(404);

    [Fact]
    public async Task A_player_who_is_not_signed_in_cannot_practise()
    {
        var (_, scenario) = await CutAsync();
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var api = new ScenariosApi(_store, practices, await AccountsAsync());

        var refused = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", new AccountRequest(null, Secure: false));

        refused.Status.ShouldBe(401);
    }

    [Fact]
    public async Task A_signed_in_player_practises_from_the_cut_in_the_scenario_s_seat_with_nothing_recorded()
    {
        var (_, scenario) = await CutAsync();
        var recorded = (await _hosted.Stored!.ListAsync(Cancel)).Count;
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts);

        var opened = Read(await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", await SignedInAsync(accounts, "learner")));

        opened.GetProperty("seat").GetString().ShouldBe("player2");
        var practice = practices.ByToken(opened.GetProperty("token").GetString()).ShouldNotBeNull();
        var view = Read(await practice.Play.Api.HandleAsync("GET", "/api/seat/player2", "", practice.Play.Token));
        view.GetProperty("board").GetProperty("roundNumber").GetInt32().ShouldBe(2);
        view.GetProperty("recording").GetBoolean().ShouldBeFalse();
        view.GetProperty("practice").GetProperty("scenario").GetString().ShouldBe(scenario);
        view.GetProperty("feed").GetArrayLength().ShouldBe(0);
        (await _hosted.Stored!.ListAsync(Cancel)).Count.ShouldBe(recorded);
    }

    /// <summary>
    /// The battle lunges a creature at its target only for a card its art says is melee (ADR 0131): a practice's
    /// catalogue carries that art as a table's does, or a tutorial's casts all play as the neutral mark, standing still.
    /// </summary>
    [Fact]
    public async Task A_practice_s_catalogue_carries_how_each_card_s_cast_is_drawn()
    {
        var (_, scenario) = await CutAsync();
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts);
        var opened = Read(await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", await SignedInAsync(accounts, "learner")));
        var practice = practices.ByToken(opened.GetProperty("token").GetString()).ShouldNotBeNull();

        var answer = await practice.Play.Api.HandleAsync("GET", "/api/catalogue", "", practice.Play.Token);

        answer.Status.ShouldBe(200);
        var guard = Read(answer).GetProperty("cards").EnumerateArray().Single(card => card.GetProperty("id").GetString() == "spell:guard:v1").GetProperty("visual");
        guard.GetProperty("family").GetString().ShouldBe("physical");
        guard.GetProperty("delivery").GetString().ShouldBe("cast");
    }

    /// <summary>
    /// A host whose bots' turns go to a queue a worker takes (the deployed shape, --turns worker): a practice is in no
    /// index the worker could find it by, so its opponent answers in this process, or the practice would wait forever.
    /// </summary>
    [Fact]
    public async Task A_practice_s_opponent_answers_in_process_on_a_host_whose_bots_turns_go_to_a_queue()
    {
        var (_, scenario) = await CutAsync();
        using var queued = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory), turns: Substitute.For<IAgentTurns>());
        await using var practices = new ScenarioPractices(queued.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts);
        var opened = Read(await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", await SignedInAsync(accounts, "learner")));
        var practice = practices.ByToken(opened.GetProperty("token").GetString()).ShouldNotBeNull();

        var asked = await AnswerSpeedsAsync(practice.Play);

        asked.ShouldNotBe("Speed", "the opponent answered its own speeds and the round moved on");
    }

    /// <summary>Answers every speed the practice's seat is asked, Quick, and says what the seat is asked after.</summary>
    private static async Task<string?> AnswerSpeedsAsync(ScenarioPlay play)
    {
        var seat = play.Seat == Domain.Matches.PlayerSlot.Player1 ? "player1" : "player2";
        async Task<JsonElement> ViewAsync() => Read(await play.Api.HandleAsync("GET", $"/api/seat/{seat}", "", play.Token));
        var view = await ViewAsync();
        foreach (var creature in view.GetProperty("options").GetProperty("speed").GetProperty("missing").EnumerateArray().Select(one => one.GetInt32()).ToList())
        {
            var asked = (await ViewAsync()).GetProperty("waitingAsked").GetInt64();
            var sent = await play.Api.HandleAsync("POST", $"/api/seat/{seat}/decision", JsonSerializer.Serialize(new { asked, kind = "Speed", creature, speed = "Quick" }), play.Token);
            sent.Status.ShouldBe(204, Encoding.UTF8.GetString(sent.Body));
        }

        for (var waited = 0; waited < 50; waited++)
        {
            view = await ViewAsync();
            if (view.GetProperty("options").GetProperty("kind").GetString() != "Speed")
            {
                break;
            }

            await Task.Delay(100, Cancel);
        }

        return view.GetProperty("options").GetProperty("kind").GetString();
    }

    [Fact]
    public async Task A_reloaded_page_resumes_the_practice_and_a_restart_replaces_it()
    {
        var (_, scenario) = await CutAsync();
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts);
        var learner = await SignedInAsync(accounts, "learner");
        var first = Read(await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", learner));

        var resumed = Read(await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{scenario}/practice", learner));
        var restarted = Read(await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", learner));

        resumed.GetProperty("token").GetString().ShouldBe(first.GetProperty("token").GetString());
        restarted.GetProperty("token").GetString().ShouldNotBe(first.GetProperty("token").GetString());
        practices.ByToken(first.GetProperty("token").GetString()).ShouldBeNull();
        // The page keeps what it showed of a game under its table's id: a resumed practice is the same game, a restart a new one.
        resumed.GetProperty("table").GetProperty("id").GetString().ShouldBe(first.GetProperty("table").GetProperty("id").GetString());
        restarted.GetProperty("table").GetProperty("id").GetString().ShouldNotBe(first.GetProperty("table").GetProperty("id").GetString());
        practices.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_practice_nobody_reads_for_an_hour_is_let_go_of()
    {
        var (_, scenario) = await CutAsync();
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts);
        var learner = await SignedInAsync(accounts, "learner");
        await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", learner);

        _clock.Advance(ScenarioPractices.Idle);
        var resumed = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{scenario}/practice", learner);

        resumed.Status.ShouldBe(404);
        practices.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Past_the_host_s_bound_a_new_practice_is_refused_until_one_ends()
    {
        var (_, scenario) = await CutAsync();
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock) { Most = 1 };
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts);
        await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", await SignedInAsync(accounts, "first"));

        var refused = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", await SignedInAsync(accounts, "second"));

        refused.Status.ShouldBe(503);
    }

    [Fact]
    public async Task A_scenario_the_operator_removed_cannot_be_practised()
    {
        var (_, scenario) = await CutAsync();
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts);

        (await Admin().HandleAsync("DELETE", $"{ScenarioAdminApi.Route}/{scenario}", "", "operator")).Status.ShouldBe(204);
        var refused = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", await SignedInAsync(accounts, "learner"));

        refused.Status.ShouldBe(404);
    }

    /// <summary>A scenario is cut off the list; listed by the operator, it is what signed-in players find under Practice (S6).</summary>
    [Fact]
    public async Task A_scenario_the_operator_lists_is_offered_to_signed_in_players_and_one_taken_off_is_not()
    {
        var (_, scenario) = await CutAsync();
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts);
        var learner = await SignedInAsync(accounts, "learner");
        var before = Read(await api.HandleAsync("GET", ScenariosApi.Prefix, learner)).GetProperty("scenarios").GetArrayLength();

        var listing = await Admin().HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{scenario}/listing", """{"listed":true}""", "operator");
        var offered = Read(await api.HandleAsync("GET", ScenariosApi.Prefix, learner)).GetProperty("scenarios").EnumerateArray().ToList();
        await Admin().HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{scenario}/listing", """{"listed":false}""", "operator");
        var after = Read(await api.HandleAsync("GET", ScenariosApi.Prefix, learner)).GetProperty("scenarios").GetArrayLength();

        before.ShouldBe(0);
        listing.Status.ShouldBe(200);
        var one = offered.ShouldHaveSingleItem();
        (one.GetProperty("id").GetString(), one.GetProperty("title").GetString(), one.GetProperty("seat").GetString(), one.GetProperty("round").GetInt32())
            .ShouldBe((scenario, "Second round", "player2", 2));
        one.GetProperty("link").GetString().ShouldBe($"/battle?scenario={scenario}");
        after.ShouldBe(0);
    }

    [Fact]
    public async Task The_operator_s_list_says_which_scenarios_are_listed()
    {
        var (_, scenario) = await CutAsync();

        await Admin().HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{scenario}/listing", """{"listed":true}""", "operator");

        Read(await Admin().HandleAsync("GET", ScenarioAdminApi.Route, "", "operator")).GetProperty("scenarios")[0].GetProperty("listed").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Listing_and_taking_off_are_noted_in_the_operators_activity()
    {
        var (_, scenario) = await CutAsync();
        // The test's clock stands still: the listing and the taking off are noted at one instant, and read in the order noted (#674).
        var actions = new OperatorActions(new FileOperatorLog(_root), _clock);
        var admin = new ScenarioAdminApi(_store, _hosted.Stored!, _hosted.Composer) { Notes = new OperatorNotes(actions) };

        await admin.HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{scenario}/listing", """{"listed":true}""", "mark");
        await admin.HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{scenario}/listing", """{"listed":false}""", "mark");

        (await actions.PageAsync(1, Cancel)).Actions.Select(one => (one.Action, one.Target)).ShouldBe([(OperatorActions.UnlistScenario, scenario), (OperatorActions.ListScenario, scenario)]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"listed":"yes"}""")]
    [InlineData("not json")]
    public async Task A_listing_that_does_not_say_listed_or_not_is_refused(string body)
    {
        var (_, scenario) = await CutAsync();

        (await Admin().HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{scenario}/listing", body, "operator")).Status.ShouldBe(400);
    }

    [Fact]
    public async Task A_scenario_the_host_does_not_keep_cannot_be_listed() =>
        (await Admin().HandleAsync("PUT", $"{ScenarioAdminApi.Route}/sc-20261114-000000-deadbeef/listing", """{"listed":true}""", "operator")).Status.ShouldBe(404);

    [Fact]
    public async Task A_player_who_is_not_signed_in_finds_no_scenarios_listed()
    {
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var api = new ScenariosApi(_store, practices, await AccountsAsync());

        (await api.HandleAsync("GET", ScenariosApi.Prefix, new AccountRequest(null, Secure: false))).Status.ShouldBe(401);
    }

    /// <summary>
    /// The repository's scenarios as the host reads them (S7), or those of <paramref name="directory" />, over a composer
    /// offered the content each carries.
    /// </summary>
    private ScenarioStore Shipping(string? directory = null)
    {
        var shipped = RepositoryScenarios.Read(directory ?? Path.Combine(AppContext.BaseDirectory, "scenarios"));
        foreach (var one in shipped.All)
        {
            _hosted.Composer.Carry(one.Document.Content, one.Document.Schema!.Value);
        }

        return new ScenarioStore(new FileArtifactStore(Path.Combine(_root, "scenarios")), _clock, shipped);
    }

    [Fact]
    public async Task A_scenario_the_repository_ships_is_listed_before_the_cut_ones_with_the_file_it_is()
    {
        var (_, cut) = await CutAsync();
        var admin = new ScenarioAdminApi(Shipping(), _hosted.Stored!, _hosted.Composer);

        var listed = Read(await admin.HandleAsync("GET", ScenarioAdminApi.Route, "", "operator")).GetProperty("scenarios");

        // The bug scenarios of scenarios/bugs/ come and go with their issues (R6): the list is pinned without them.
        listed.EnumerateArray().Where(one => !IsBug(one)).Select(one => (one.GetProperty("id").GetString(), one.GetProperty("source").GetString(), one.GetProperty("file").GetString()))
            .ShouldBe([
                ("sc-repo-greedy-1v1-round-3", "repository", "greedy-1v1-round-3.json"),
                ("sc-repo-tutorial-grow-by-mutating", "repository", "tutorials/grow-by-mutating.json"),
                ("sc-repo-tutorial-to-the-end", "repository", "tutorials/to-the-end.json"),
                ("sc-repo-tutorial-who-acts-first", "repository", "tutorials/who-acts-first.json"),
                ("sc-repo-tutorial-your-first-combat", "repository", "tutorials/your-first-combat.json"),
                (cut, "cut", null),
            ]);
        listed[0].GetProperty("link").GetString().ShouldBe("/battle?scenario=sc-repo-greedy-1v1-round-3");
    }

    [Fact]
    public async Task A_scenario_the_repository_ships_is_practised_on_the_content_it_carries()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(store, practices, accounts);

        var answer = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/sc-repo-greedy-1v1-round-3/practice", await SignedInAsync(accounts, "learner"));

        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        var opened = Read(answer);
        opened.GetProperty("seat").GetString().ShouldBe("player1");
        var practice = practices.ByToken(opened.GetProperty("token").GetString()).ShouldNotBeNull();
        var view = Read(await practice.Play.Api.HandleAsync("GET", "/api/seat/player1", "", practice.Play.Token));
        view.GetProperty("board").GetProperty("roundNumber").GetInt32().ShouldBe(3);
        view.GetProperty("recording").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_scenario_the_repository_ships_is_neither_removed_nor_listed_under_Practice_at_the_host()
    {
        var store = Shipping();
        var admin = new ScenarioAdminApi(store, _hosted.Stored!, _hosted.Composer);
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();

        var removed = await admin.HandleAsync("DELETE", $"{ScenarioAdminApi.Route}/sc-repo-greedy-1v1-round-3", "", "operator");
        var listing = await admin.HandleAsync("PUT", $"{ScenarioAdminApi.Route}/sc-repo-greedy-1v1-round-3/listing", """{"listed":true}""", "operator");
        var offered = Read(await new ScenariosApi(store, practices, accounts).HandleAsync("GET", ScenariosApi.Prefix, await SignedInAsync(accounts, "learner")));

        (removed.Status, listing.Status).ShouldBe((409, 409));
        Read(removed).GetProperty("error").GetString().ShouldBe("Scenario.FromRepository");
        (await store.GetAsync("sc-repo-greedy-1v1-round-3", Cancel)).ShouldNotBeNull();
        offered.GetProperty("scenarios").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task The_operator_s_list_names_the_issue_a_bug_scenario_reproduces_and_no_issue_for_the_others()
    {
        var shipped = BugsShipped();
        var (_, cut) = await CutAsync();
        var admin = new ScenarioAdminApi(Shipping(shipped), _hosted.Stored!, _hosted.Composer);

        var listed = Read(await admin.HandleAsync("GET", ScenarioAdminApi.Route, "", "operator")).GetProperty("scenarios").EnumerateArray().ToList();

        listed.Select(one => (one.GetProperty("id").GetString(), one.GetProperty("bug").ValueKind == JsonValueKind.Null ? (int?)null : one.GetProperty("bug").GetInt32()))
            .ShouldBe([("sc-repo-plain", null), ("sc-repo-bug-812-a-bleed", 812), (cut, null)]);
    }

    [Fact]
    public async Task On_a_host_that_names_admin_accounts_only_they_open_a_bug_scenario()
    {
        var store = Shipping(BugsShipped());
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();
        var admin = await SignedInAsync(accounts, "keeper");
        var keeper = (await accounts.WhoAsync(admin, Cancel)).ShouldNotBeNull().Account.Id;
        var api = new ScenariosApi(store, practices, accounts) { Admins = new HashSet<SharedKernel.Identifiers.AccountId> { keeper } };

        var refused = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/sc-repo-bug-812-a-bleed/practice", await SignedInAsync(accounts, "learner"));
        var opened = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/sc-repo-bug-812-a-bleed/practice", admin);

        (refused.Status, opened.Status).ShouldBe((404, 200));
    }

    /// <summary>A scenarios directory of its own: one plain file, and the same one as the bug scenario of issue 812.</summary>
    private string BugsShipped()
    {
        var directory = Path.Combine(_root, "shipped");
        Directory.CreateDirectory(Path.Combine(directory, "bugs"));
        var greedy = Path.Combine(AppContext.BaseDirectory, "scenarios", "greedy-1v1-round-3.json");
        File.Copy(greedy, Path.Combine(directory, "plain.json"));
        File.Copy(greedy, Path.Combine(directory, "bugs", "812-a-bleed.json"));
        return directory;
    }

    private static bool IsBug(JsonElement row) => row.GetProperty("bug").ValueKind != JsonValueKind.Null;

    [Fact]
    public async Task On_a_host_that_names_admin_accounts_only_they_open_a_scenario_the_repository_ships()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();
        var admin = await SignedInAsync(accounts, "keeper");
        var keeper = (await accounts.WhoAsync(admin, Cancel)).ShouldNotBeNull().Account.Id;
        var api = new ScenariosApi(store, practices, accounts) { Admins = new HashSet<SharedKernel.Identifiers.AccountId> { keeper } };

        var refused = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/sc-repo-greedy-1v1-round-3/practice", await SignedInAsync(accounts, "learner"));
        var described = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/sc-repo-greedy-1v1-round-3", await SignedInAsync(accounts, "learner"));
        var opened = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/sc-repo-greedy-1v1-round-3/practice", admin);

        (refused.Status, described.Status, opened.Status).ShouldBe((404, 404, 200));
    }

    private const string Tutorial = "sc-repo-tutorial-your-first-combat";

    /// <summary>The series the repository ships (docs/tabletop/onboarding-plan.md, O5), in its order.</summary>
    private static readonly string[] Series = [Tutorial, "sc-repo-tutorial-who-acts-first", "sc-repo-tutorial-grow-by-mutating", "sc-repo-tutorial-to-the-end"];

    [Fact]
    public async Task A_signed_in_account_finds_the_tutorials_in_series_order_with_their_links()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(store, practices, accounts);

        var answer = await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, await SignedInAsync(accounts, "learner"));

        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        var series = Read(answer).GetProperty("tutorials").EnumerateArray().ToList();
        series.Select(one => (one.GetProperty("id").GetString(), one.GetProperty("order").GetInt32())).ShouldBe(Series.Select((id, index) => ((string?)id, index + 1)));
        var listed = series[0];
        (listed.GetProperty("id").GetString(), listed.GetProperty("order").GetInt32(), listed.GetProperty("of").GetInt32(), listed.GetProperty("title").GetString())
            .ShouldBe((Tutorial, 1, 4, "Your first combat"));
        (listed.GetProperty("round").GetInt32(), listed.GetProperty("seat").GetString(), listed.GetProperty("link").GetString())
            .ShouldBe((2, "player1", $"/battle?scenario={Tutorial}"));
        listed.TryGetProperty("goal", out _).ShouldBeTrue();
        listed.TryGetProperty("format", out _).ShouldBeTrue();
        ScenariosApi.Names(ScenariosApi.TutorialsRoute).ShouldBeTrue();
    }

    [Fact]
    public async Task On_a_host_that_names_admin_accounts_a_player_who_is_not_one_still_finds_the_tutorials()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();
        var keeper = (await accounts.WhoAsync(await SignedInAsync(accounts, "keeper"), Cancel)).ShouldNotBeNull().Account.Id;
        var api = new ScenariosApi(store, practices, accounts) { Admins = new HashSet<SharedKernel.Identifiers.AccountId> { keeper } };

        var answer = await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, await SignedInAsync(accounts, "learner"));

        answer.Status.ShouldBe(200);
        Read(answer).GetProperty("tutorials").EnumerateArray().Select(one => one.GetProperty("id").GetString()).ShouldBe(Series);
    }

    [Fact]
    public async Task A_player_who_is_not_signed_in_finds_no_tutorials()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var api = new ScenariosApi(store, practices, await AccountsAsync());

        var refused = await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, new AccountRequest(null, Secure: false));

        refused.Status.ShouldBe(401);
        Read(refused).GetProperty("error").GetString().ShouldBe("Account.SignInRequired");
    }

    [Fact]
    public async Task A_session_due_for_renewal_is_renewed_on_a_list_and_on_a_scenario()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = new AccountApi(new AccountRegistry(_accounts, _clock), new AccountSessions(await _accounts.SessionKeyAsync(Cancel), _clock), AccountsMode.Development);
        var api = new ScenariosApi(store, practices, accounts);
        var learner = await SignedInAsync(accounts, "learner");
        _clock.Advance(AccountSessions.RenewAfter);

        var listed = await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner);
        var described = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}", learner);

        static string? Renewed(StudioResponse answer) => answer.Headers?.SingleOrDefault(header => header.Key == "Set-Cookie").Value;
        (listed.Status, described.Status).ShouldBe((200, 200));
        Renewed(listed).ShouldNotBeNull().ShouldStartWith(AccountSessions.Cookie + "=");
        Renewed(described).ShouldNotBeNull().ShouldStartWith(AccountSessions.Cookie + "=");
    }

    [Fact]
    public async Task A_host_that_ships_no_tutorial_lists_none()
    {
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts);

        var answer = await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, await SignedInAsync(accounts, "learner"));

        answer.Status.ShouldBe(200);
        Read(answer).GetProperty("tutorials").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task The_tutorials_route_only_answers_a_read()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(store, practices, accounts);

        var refused = await api.HandleAsync("POST", ScenariosApi.TutorialsRoute, await SignedInAsync(accounts, "learner"));

        refused.Status.ShouldBe(404);
        Read(refused).GetProperty("error").GetString().ShouldBe("Scenario.NoRoute");
    }

    [Fact]
    public async Task A_tutorial_is_not_listed_under_Practice_and_is_neither_removed_nor_listed_by_the_operator()
    {
        var store = Shipping();
        var admin = new ScenarioAdminApi(store, _hosted.Stored!, _hosted.Composer);
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();

        var removed = await admin.HandleAsync("DELETE", $"{ScenarioAdminApi.Route}/{Tutorial}", "", "operator");
        var listing = await admin.HandleAsync("PUT", $"{ScenarioAdminApi.Route}/{Tutorial}/listing", """{"listed":true}""", "operator");
        var offered = Read(await new ScenariosApi(store, practices, accounts).HandleAsync("GET", ScenariosApi.Prefix, await SignedInAsync(accounts, "learner")));

        (removed.Status, listing.Status).ShouldBe((409, 409));
        Read(removed).GetProperty("error").GetString().ShouldBe("Scenario.FromRepository");
        Read(listing).GetProperty("error").GetString().ShouldBe("Scenario.FromRepository");
        offered.GetProperty("scenarios").EnumerateArray().Select(one => one.GetProperty("id").GetString()).ShouldNotContain(Tutorial);
    }

    [Fact]
    public async Task On_a_host_that_names_admin_accounts_any_account_opens_and_reads_a_tutorial_but_not_the_other_shipped_scenario()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();
        var keeper = (await accounts.WhoAsync(await SignedInAsync(accounts, "keeper"), Cancel)).ShouldNotBeNull().Account.Id;
        var api = new ScenariosApi(store, practices, accounts) { Admins = new HashSet<SharedKernel.Identifiers.AccountId> { keeper } };
        var learner = await SignedInAsync(accounts, "learner");

        var opened = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);
        var described = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}", learner);
        var plain = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/sc-repo-greedy-1v1-round-3", learner);

        (opened.Status, described.Status, plain.Status).ShouldBe((200, 200, 404));
    }

    [Fact]
    public async Task The_operator_s_list_numbers_a_tutorial_in_its_series_and_the_other_scenarios_not_at_all()
    {
        var (_, cut) = await CutAsync();
        var admin = new ScenarioAdminApi(Shipping(), _hosted.Stored!, _hosted.Composer);

        var listed = Read(await admin.HandleAsync("GET", ScenarioAdminApi.Route, "", "operator")).GetProperty("scenarios").EnumerateArray().ToList();

        listed.Where(one => !IsBug(one)).Select(one => (one.GetProperty("id").GetString(), one.GetProperty("tutorial").ValueKind == JsonValueKind.Null ? (int?)null : one.GetProperty("tutorial").GetInt32()))
            .ShouldBe([("sc-repo-greedy-1v1-round-3", null), ("sc-repo-tutorial-grow-by-mutating", 3), ("sc-repo-tutorial-to-the-end", 4), ("sc-repo-tutorial-who-acts-first", 2), (Tutorial, 1), (cut, null)]);
    }

    [Fact]
    public async Task A_tutorial_practice_answers_what_is_not_taught_and_stops_at_the_first_question_it_teaches()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var play = await OpenTutorialAsync(store, practices, "learner");

        var view = await WaitingForAsync(play, one => one.GetProperty("waitingFor").GetString() == "Action");

        view.GetProperty("waitingFor").GetString().ShouldBe("Action");
        (view.GetProperty("board").GetProperty("roundNumber").GetInt32(), view.GetProperty("waitingCreature").GetInt32()).ShouldBe((2, 2));
        var tutorial = view.GetProperty("practice").GetProperty("tutorial");
        (tutorial.GetProperty("order").GetInt32(), tutorial.GetProperty("of").GetInt32(), tutorial.GetProperty("title").GetString()).ShouldBe((1, 4, "Your first combat"));
        tutorial.GetProperty("teaches").EnumerateArray().Select(one => one.GetString()).ShouldBe(["Action"]);
        tutorial.GetProperty("assisted").EnumerateArray().Select(one => one.GetString()).ShouldBe(["Evolution", "Speed", "TieOrder", "Intent", "Target"]);
        tutorial.GetProperty("steps").GetArrayLength().ShouldBe(3);
        var step = tutorial.GetProperty("steps")[0];
        step.GetProperty("at").GetProperty("kind").GetString().ShouldBe("Action");
        step.GetProperty("at").GetProperty("round").GetInt32().ShouldBe(2);
        step.GetProperty("at").GetProperty("creature").GetInt32().ShouldBe(2);
        step.GetProperty("says").GetArrayLength().ShouldBeGreaterThan(0);
        step.GetProperty("concepts").EnumerateArray().Select(one => one.GetString()).ShouldContain("health");
        step.GetProperty("pointsAt").GetProperty("creature").GetInt32().ShouldBe(4);
        step.GetProperty("pointsAt").GetProperty("control").ValueKind.ShouldBe(JsonValueKind.Null);
        (tutorial.GetProperty("shown").GetInt32(), tutorial.GetProperty("done").GetBoolean()).ShouldBe((1, false));
    }

    [Fact]
    public async Task A_tutorial_is_done_once_the_person_has_answered_each_question_it_teaches()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var play = await OpenTutorialAsync(store, practices, "learner");
        var recorded = await AnswerTheLessonAsync(store, play);

        var done = await WaitingForAsync(play, one => one.GetProperty("practice").GetProperty("tutorial").GetProperty("done").GetBoolean());
        recorded.Count.ShouldBe(3);
        done.GetProperty("practice").GetProperty("tutorial").GetProperty("shown").GetInt32().ShouldBe(3);
        done.GetProperty("over").GetBoolean().ShouldBeFalse("the last step's round ended, not the match");
    }

    [Fact]
    public async Task A_tutorial_done_answers_nothing_more_for_the_person_so_the_match_waits_at_the_next_round()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);

        var done = await WaitingForAsync(play, one => one.GetProperty("practice").GetProperty("tutorial").GetProperty("done").GetBoolean()
            && one.GetProperty("waitingFor").ValueKind == JsonValueKind.String);
        await Task.Delay(500, Cancel);
        var later = await WaitingForAsync(play, _ => true);

        // The next round's first question (its mutations) is an assisted one: before, it was answered, and so were the
        // tempos after it, and that round's combat played on under the tutorial's end.
        (done.GetProperty("board").GetProperty("roundNumber").GetInt32(), done.GetProperty("board").GetProperty("phase").GetString()).ShouldBe((3, "Planning"));
        (later.GetProperty("waitingFor").GetString(), later.GetProperty("waitingAsked").GetInt64()).ShouldBe((done.GetProperty("waitingFor").GetString(), done.GetProperty("waitingAsked").GetInt64()));
    }

    [Fact]
    public async Task A_finished_tutorial_is_not_resumed_and_opens_from_its_cut_the_next_time()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, one => one.GetProperty("practice").GetProperty("tutorial").GetProperty("done").GetBoolean());
        (await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/done", learner)).Status.ShouldBe(200);

        var reopened = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);

        reopened.Status.ShouldBe(404);
        practices.Count.ShouldBe(0);
        practices.ByToken(play.Token).ShouldBeNull();
    }

    /// <summary>
    /// A tutorial whose table was let go of did not end its match, so it is not done, and it is not resumed on a board
    /// with nothing left to play. Its practice is let go of, and the next time it opens it starts from its cut, with
    /// nothing kept on the account (#912).
    /// </summary>
    [Fact]
    public async Task A_tutorial_whose_table_was_let_go_of_is_not_resumed_and_opens_from_its_cut_the_next_time()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");
        play.Session.Dispose();
        await Task.WhenAny(play.Session.Outcome);

        var reopened = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);

        reopened.Status.ShouldBe(404);
        practices.Count.ShouldBe(0);
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).State.ShouldBe("new");
    }

    [Fact]
    public async Task Leaving_a_practice_lets_go_of_it_and_the_next_one_starts_from_the_cut()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");

        var left = await api.HandleAsync("DELETE", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);
        var again = await api.HandleAsync("DELETE", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);
        var resumed = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);
        var nobody = await api.HandleAsync("DELETE", $"{ScenariosApi.Prefix}/{Tutorial}/practice", new AccountRequest(null, Secure: false));

        (left.Status, again.Status, resumed.Status, nobody.Status).ShouldBe((204, 204, 404, 401));
        practices.Count.ShouldBe(0);
        practices.ByToken(play.Token).ShouldBeNull();
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("GET")]
    public async Task A_finished_tutorial_let_go_of_before_the_page_said_done_is_kept_done(string method)
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, one => one.GetProperty("practice").GetProperty("tutorial").GetProperty("done").GetBoolean());

        // A Leave, or a reload, reaches the host before the page's own word that the tutorial is done.
        await api.HandleAsync(method, $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);

        practices.Count.ShouldBe(0);
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "done"));
    }

    /// <summary>
    /// A reload reads once whether the tutorial is done, under the practices' gate, and lets its practice go on that
    /// same read, keeping it done first. Read twice, a match that ended between the two reads was let go of unkept,
    /// and the page's later word that it is done was refused (#931). Here the match ends after the first look at it.
    /// </summary>
    [Fact]
    public async Task A_reload_lets_a_tutorial_go_only_on_the_read_that_kept_it_done()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");
        var tutorial = play.Api.Practice.ShouldNotBeNull().Tutorial.ShouldNotBeNull();
        var looks = new int[1];
        tutorial.Follow(new TutorialPlay.Seat(() => null, () => Interlocked.Increment(ref looks[0]) > 1, (_, _) => throw new InvalidOperationException("Nothing is answered.")));

        var playing = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);
        var states = Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).State;
        var ended = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);

        (playing.Status, states).ShouldBe((200, "new"));
        ended.Status.ShouldBe(404);
        practices.Count.ShouldBe(0);
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "done"));
    }

    /// <summary>
    /// Keeping a tutorial done is a store write: it is made outside the gate every account's practices wait on, so a
    /// slow store holds no one else's practice, and the tutorial is let go of once it is kept.
    /// </summary>
    [Fact]
    public async Task Keeping_a_tutorial_done_holds_no_other_account_s_practice_and_lets_it_go_once_kept()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, one => one.GetProperty("practice").GetProperty("tutorial").GetProperty("done").GetBoolean());
        var account = practices.ByToken(play.Token).ShouldNotBeNull().Account;
        var kept = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var resuming = practices.ResumeAsync(account, Tutorial, (_, _) => { writing.TrySetResult(); return kept.Task; });
        await writing.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        var other = await practices.CurrentAsync("someone-else", Tutorial).WaitAsync(TimeSpan.FromSeconds(5), Cancel);
        kept.SetResult();

        other.ShouldBeNull();
        (await resuming).ShouldBeNull();
        practices.Count.ShouldBe(0);
    }

    /// <summary>
    /// A Leave reads the tutorial not done, and its match ends before the practice is let go of: it is read again where
    /// it is let go of, and kept done first (#931).
    /// </summary>
    [Fact]
    public async Task A_tutorial_whose_match_ends_while_it_is_left_is_kept_done()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");
        var looks = new int[1];
        play.Api.Practice!.Tutorial!.Follow(new TutorialPlay.Seat(() => null, () => Interlocked.Increment(ref looks[0]) > 1, (_, _) => throw new InvalidOperationException("Nothing is answered.")));

        var left = await api.HandleAsync("DELETE", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);

        left.Status.ShouldBe(204);
        practices.Count.ShouldBe(0);
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "done"));
    }

    /// <summary>The account opens the tutorial again while its done is being kept: the new practice is not let go of.</summary>
    [Fact]
    public async Task A_practice_opened_while_the_last_one_is_kept_done_is_not_let_go_of()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, one => one.GetProperty("practice").GetProperty("tutorial").GetProperty("done").GetBoolean());
        var account = practices.ByToken(play.Token).ShouldNotBeNull().Account;
        var kept = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var resuming = practices.ResumeAsync(account, Tutorial, (_, _) => { writing.TrySetResult(); return kept.Task; });
        await writing.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        var (opened, _) = await practices.OpenAsync(Tutorial, account, cancellationToken: Cancel);
        kept.SetResult();
        await resuming;

        practices.ByToken(opened.ShouldNotBeNull().Play.Token).ShouldNotBeNull();
        practices.ByToken(play.Token).ShouldBeNull();
    }

    /// <summary>
    /// A store that cannot keep the tutorial done answers 503 with a code of its own, not a bug's 500, and the practice
    /// is kept for the next reload or Leave.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task A_tutorial_done_that_the_store_cannot_keep_answers_unavailable_and_is_kept_for_the_next_try(string method)
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();
        var preferences = Substitute.For<Application.Accounts.Ports.IPreferenceStore>();
        preferences.GetTutorialsAsync(Arg.Any<SharedKernel.Identifiers.AccountId>(), Arg.Any<CancellationToken>()).Returns<Task<TutorialProgress>>(_ => throw new IOException("The store is out."));
        accounts.Preferences = preferences;
        var said = new List<string>();
        var api = new ScenariosApi(store, practices, accounts) { Said = said.Add };
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, one => one.GetProperty("practice").GetProperty("tutorial").GetProperty("done").GetBoolean());

        var answer = await api.HandleAsync(method, $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);

        answer.Status.ShouldBe(503);
        Read(answer).GetProperty("error").GetString().ShouldBe("Tutorial.Unavailable");
        Encoding.UTF8.GetString(answer.Body).ShouldNotContain("The store is out.");
        said.ShouldContain(line => line.Contains("could not be kept", StringComparison.Ordinal));
        practices.ByToken(play.Token).ShouldNotBeNull();
    }

    /// <summary>
    /// Opening a practice stops the account's last one: Next opens the following lesson, Play again the same one from
    /// its cut. A tutorial done is kept done on the account first, whatever the page's own word reached (#1138).
    /// </summary>
    [Theory]
    [InlineData(Tutorial)]
    [InlineData("sc-repo-tutorial-who-acts-first")]
    public async Task A_tutorial_done_is_kept_done_before_the_next_practice_opened_replaces_it(string opened)
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, Finished);

        var answer = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{opened}/practice", learner);

        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        practices.ByToken(play.Token).ShouldBeNull();
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "done"));
    }

    /// <summary>
    /// A store that cannot keep the last tutorial done refuses the new practice with 503, as a Leave does, and the
    /// done practice stays: the page's next try keeps it done and opens the new one (#1138).
    /// </summary>
    [Fact]
    public async Task A_practice_opened_while_the_store_cannot_keep_the_last_tutorial_done_is_refused_until_it_can()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts, preferences) = await FailingOnceAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, Finished);
        preferences.Failures = 1;

        var refused = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);
        var kept = practices.Count;
        var opened = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);

        (refused.Status, Read(refused).GetProperty("error").GetString(), kept).ShouldBe((503, "Tutorial.Unavailable", 1));
        opened.Status.ShouldBe(200, Encoding.UTF8.GetString(opened.Body));
        practices.ByToken(play.Token).ShouldBeNull();
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "done"));
    }

    /// <summary>
    /// A tutorial done that nobody reads for an hour is kept done on its account before the sweep lets it go, after
    /// whoever's request swept it, and tried at least once however long the host stayed quiet (#1138).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task A_tutorial_done_nobody_reads_for_an_hour_is_kept_done_before_it_is_let_go_of(int hours)
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var other = await SignedInAsync(accounts, "other");
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, Finished);
        _clock.Advance(TimeSpan.FromHours(hours));

        var swept = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", other);
        await practices.Kept.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        swept.Status.ShouldBe(404);
        practices.Count.ShouldBe(0);
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "done"));
    }

    /// <summary>
    /// The sweep cannot keep a tutorial done: it says so on the console and keeps the practice, without failing the
    /// request that swept it, and the next sweep keeps it done and lets it go (#1138).
    /// </summary>
    [Fact]
    public async Task A_tutorial_done_the_sweep_cannot_keep_is_kept_for_the_next_sweep()
    {
        var store = Shipping();
        var said = new List<string>();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock) { Said = said.Add };
        var (api, accounts, preferences) = await FailingOnceAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var other = await SignedInAsync(accounts, "other");
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, Finished);
        _clock.Advance(ScenarioPractices.Idle);
        preferences.Failures = 1;

        var first = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", other);
        await practices.Kept.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        var kept = practices.Count;
        _clock.Advance(ScenarioPractices.RetryAfter);
        var second = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", other);
        await practices.Kept.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        (first.Status, kept, second.Status).ShouldBe((404, 1, 404));
        said.ShouldContain(line => line.Contains("could not be kept", StringComparison.Ordinal));
        practices.Count.ShouldBe(0);
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "done"));
    }

    /// <summary>
    /// A store that keeps failing for one account does not hold its practice forever: once its keeping has failed for
    /// <see cref="ScenarioPractices.KeptFor" />, counted from the first failed try, the sweep lets it go unkept and
    /// says so (#1138).
    /// </summary>
    [Fact]
    public async Task A_tutorial_done_the_store_never_keeps_is_let_go_of_unkept_once_its_keeping_failed_for_long_enough()
    {
        var store = Shipping();
        var said = new List<string>();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock) { Said = said.Add };
        var (api, accounts, preferences) = await FailingOnceAsync(store, practices);
        var other = await SignedInAsync(accounts, "other");
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, Finished);
        _clock.Advance(ScenarioPractices.Idle);
        preferences.Failures = int.MaxValue;
        await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", other);
        await practices.Kept.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        var heldAfterOneFailure = practices.Count;
        _clock.Advance(ScenarioPractices.KeptFor);

        await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", other);
        await practices.Kept.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        heldAfterOneFailure.ShouldBe(1);
        practices.Count.ShouldBe(0);
        said.ShouldContain(line => line.Contains("let go of unkept", StringComparison.Ordinal));
    }

    /// <summary>
    /// A failure from an idle stretch the player ended by coming back does not count against the next one: the next
    /// sweep tries again, and keeps the tutorial done (#1138).
    /// </summary>
    [Fact]
    public async Task A_tutorial_read_after_a_failed_keeping_is_tried_again_in_its_next_idle_stretch()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock) { Said = _ => { } };
        var (api, accounts, preferences) = await FailingOnceAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var other = await SignedInAsync(accounts, "other");
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, Finished);
        _clock.Advance(ScenarioPractices.Idle);
        preferences.Failures = 1;
        await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", other);
        await practices.Kept.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        _clock.Advance(TimeSpan.FromMinutes(5));
        practices.ByToken(play.Token).ShouldNotBeNull();
        _clock.Advance(ScenarioPractices.Idle + ScenarioPractices.KeptFor);

        await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", other);
        await practices.Kept.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        practices.Count.ShouldBe(0);
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "done"));
    }

    /// <summary>Another account's request is answered while the sweep's write is still on its way: it waits on no other account's store (#1138).</summary>
    [Fact]
    public async Task A_request_that_sweeps_answers_before_the_swept_tutorial_is_kept()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, Finished);
        _clock.Advance(ScenarioPractices.Idle);
        var kept = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var current = await practices.CurrentAsync("someone-else", Tutorial, (_, _) => kept.Task).WaitAsync(TimeSpan.FromSeconds(5), Cancel);
        var stillHeld = practices.Count;
        kept.SetResult();
        await practices.Kept.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        (current, stillHeld).ShouldBe((null, 1));
        practices.Count.ShouldBe(0);
    }

    /// <summary>Opening a practice sweeps too: another account's tutorial done and idle is kept done before it goes (#1138).</summary>
    [Fact]
    public async Task Opening_a_practice_keeps_another_account_s_idle_tutorial_done_before_it_is_let_go_of()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var other = await SignedInAsync(accounts, "other");
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, Finished);
        _clock.Advance(ScenarioPractices.Idle);

        var opened = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{Series[1]}/practice", other);
        await practices.Kept.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        opened.Status.ShouldBe(200, Encoding.UTF8.GetString(opened.Body));
        practices.ByToken(play.Token).ShouldBeNull();
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "done"));
    }

    /// <summary>
    /// What one account's write throws, of any kind, is said on the console: the request of another account that swept
    /// it is answered as if nothing happened (#1138).
    /// </summary>
    [Fact]
    public async Task A_sweep_whose_keeping_throws_fails_no_other_account_s_request()
    {
        var store = Shipping();
        var said = new List<string>();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock) { Said = said.Add };
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, Finished);
        _clock.Advance(ScenarioPractices.Idle);

        var current = await practices.CurrentAsync("someone-else", Tutorial, (_, _) => throw new InvalidDataException("A broken account record."));
        await practices.Kept.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        current.ShouldBeNull();
        practices.Count.ShouldBe(1);
        said.ShouldContain(line => line.Contains("could not be kept", StringComparison.Ordinal));
    }

    /// <summary>A tutorial read while the sweep keeps it done is kept done and not let go of: it is no longer idle.</summary>
    [Fact]
    public async Task A_tutorial_read_while_the_sweep_keeps_it_done_is_not_let_go_of()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, Finished);
        _clock.Advance(ScenarioPractices.Idle);
        var kept = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await practices.CurrentAsync("someone-else", Tutorial, (_, _) => { writing.TrySetResult(); return kept.Task; });
        await writing.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        practices.ByToken(play.Token).ShouldNotBeNull();
        kept.SetResult();
        await practices.Kept.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        practices.ByToken(play.Token).ShouldNotBeNull();
    }

    /// <summary>
    /// A store that cannot say whether the other tutorials are done opens the lesson all the same: the view says they
    /// are not, and the console says why (#980).
    /// </summary>
    [Fact]
    public async Task A_tutorial_opened_while_the_store_cannot_say_the_others_are_done_says_they_are_not()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();
        var preferences = new FailingPreferences(new FilePreferenceStore(_root)) { Failures = 1 };
        accounts.Preferences = preferences;
        var said = new List<string>();
        var api = new ScenariosApi(store, practices, accounts) { Said = said.Add };
        var learner = await SignedInAsync(accounts, "learner");

        var last = await OpenedPlayAsync(api, practices, learner, Series[^1]);

        Lesson(await last.Api.HandleAsync("GET", "/api/seat/player1", "", last.Token)).GetProperty("othersDone").GetBoolean().ShouldBeFalse();
        said.ShouldContain(line => line.Contains("other tutorials could not be read", StringComparison.Ordinal));
    }

    /// <summary>
    /// A tutorial missing from the series at this host: the lesson before it names the one after, and the last does not
    /// say the series is finished, whatever the account did (#980).
    /// </summary>
    [Fact]
    public async Task A_series_missing_a_lesson_names_the_one_after_the_gap_and_is_never_finished()
    {
        var store = ShippingAllBut("who-acts-first.json");
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var id = (await accounts.WhoAsync(learner, Cancel)).ShouldNotBeNull().Account.Id;
        await accounts.NoteTutorialAsync(id, Series[0], TutorialState.Done, Cancel);
        await accounts.NoteTutorialAsync(id, Series[2], TutorialState.Done, Cancel);

        var first = await OpenedPlayAsync(api, practices, learner, Series[0]);
        var next = Lesson(await first.Api.HandleAsync("GET", "/api/seat/player1", "", first.Token)).GetProperty("next");
        var last = await OpenedPlayAsync(api, practices, learner, Series[^1]);

        next.GetProperty("link").GetString().ShouldBe($"/battle?scenario={Series[2]}");
        Lesson(await last.Api.HandleAsync("GET", "/api/seat/player1", "", last.Token)).GetProperty("othersDone").GetBoolean().ShouldBeFalse();
    }

    /// <summary>The repository's scenarios, but for one tutorial file left out.</summary>
    private ScenarioStore ShippingAllBut(string left)
    {
        var directory = Path.Combine(_root, "gap");
        var tutorials = Path.Combine(directory, RepositoryScenarios.TutorialsDirectory);
        Directory.CreateDirectory(tutorials);
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "scenarios", RepositoryScenarios.TutorialsDirectory)).Where(file => Path.GetFileName(file) != left))
        {
            File.Copy(file, Path.Combine(tutorials, Path.GetFileName(file)));
        }

        return Shipping(directory);
    }

    /// <summary>
    /// The seat's view names the lesson that follows, its title and its link, so the end card needs no second request
    /// (#980); the last of the series names none.
    /// </summary>
    [Fact]
    public async Task A_tutorial_s_view_names_the_next_lesson_and_the_last_names_none()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var first = await OpenTutorialAsync(store, practices, "learner");
        var next = Lesson(await first.Api.HandleAsync("GET", "/api/seat/player1", "", first.Token)).GetProperty("next");
        var last = await OpenedPlayAsync(api, practices, learner, Series[^1]);

        (next.GetProperty("title").GetString(), next.GetProperty("link").GetString()).ShouldBe(("Who acts first", $"/battle?scenario={Series[1]}"));
        Lesson(await last.Api.HandleAsync("GET", "/api/seat/player1", "", last.Token)).GetProperty("next").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>
    /// The seat's view says whether the account had done every other tutorial when this one opened: the last lesson's
    /// end says the series is finished from it, with no second request (#980). One skipped is not done.
    /// </summary>
    [Theory]
    [InlineData(TutorialState.Done, true)]
    [InlineData(TutorialState.Skipped, false)]
    public async Task A_tutorial_s_view_says_whether_every_other_tutorial_was_done(TutorialState earlier, bool othersDone)
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var id = (await accounts.WhoAsync(learner, Cancel)).ShouldNotBeNull().Account.Id;
        foreach (var one in Series[..^2])
        {
            await accounts.NoteTutorialAsync(id, one, TutorialState.Done, Cancel);
        }

        await accounts.NoteTutorialAsync(id, Series[^2], earlier, Cancel);

        var last = await OpenedPlayAsync(api, practices, learner, Series[^1]);

        Lesson(await last.Api.HandleAsync("GET", "/api/seat/player1", "", last.Token)).GetProperty("othersDone").GetBoolean().ShouldBe(othersDone);
    }

    private static JsonElement Lesson(StudioResponse view) => Read(view).GetProperty("practice").GetProperty("tutorial");

    /// <summary>The scenario opened as a practice through the route, by the signed-in player.</summary>
    private static async Task<ScenarioPlay> OpenedPlayAsync(ScenariosApi api, ScenarioPractices practices, AccountRequest player, string id)
    {
        var answer = await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{id}/practice", player);
        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        return practices.ByToken(Read(answer).GetProperty("token").GetString()).ShouldNotBeNull().Play;
    }

    /// <summary>The scenarios' API over accounts whose game settings are kept by a store that fails as often as it is told.</summary>
    private async Task<(ScenariosApi Api, AccountApi Accounts, FailingPreferences Preferences)> FailingOnceAsync(ScenarioStore store, ScenarioPractices practices)
    {
        var accounts = await AccountsAsync();
        var preferences = new FailingPreferences(new FilePreferenceStore(_root));
        accounts.Preferences = preferences;
        return (new ScenariosApi(store, practices, accounts) { Said = _ => { } }, accounts, preferences);
    }

    /// <summary>A preference store whose tutorials' read fails <see cref="Failures" /> times, then reads as the one it wraps.</summary>
    private sealed class FailingPreferences(Application.Accounts.Ports.IPreferenceStore inner) : Application.Accounts.Ports.IPreferenceStore
    {
        public int Failures { get; set; }

        public Task<GamePreferences?> GetAsync(SharedKernel.Identifiers.AccountId account, CancellationToken cancellationToken = default) => inner.GetAsync(account, cancellationToken);

        public Task SaveAsync(SharedKernel.Identifiers.AccountId account, GamePreferences preferences, CancellationToken cancellationToken = default) => inner.SaveAsync(account, preferences, cancellationToken);

        public Task<TutorialProgress> GetTutorialsAsync(SharedKernel.Identifiers.AccountId account, CancellationToken cancellationToken = default)
        {
            if (Failures > 0)
            {
                Failures--;
                throw new IOException("The store is out.");
            }

            return inner.GetTutorialsAsync(account, cancellationToken);
        }

        public Task SaveTutorialsAsync(SharedKernel.Identifiers.AccountId account, TutorialProgress tutorials, CancellationToken cancellationToken = default) => inner.SaveTutorialsAsync(account, tutorials, cancellationToken);

        public Task<NoticePreferences> GetNoticesAsync(SharedKernel.Identifiers.AccountId account, CancellationToken cancellationToken = default) => inner.GetNoticesAsync(account, cancellationToken);

        public Task SaveNoticesAsync(SharedKernel.Identifiers.AccountId account, NoticePreferences notices, CancellationToken cancellationToken = default) => inner.SaveNoticesAsync(account, notices, cancellationToken);

        public Task ForgetAsync(SharedKernel.Identifiers.AccountId account, CancellationToken cancellationToken = default) => inner.ForgetAsync(account, cancellationToken);
    }

    /// <summary>
    /// A tutorial whose session could not go on (a journal that refuses the bots' writes) has no outcome: it plays on no more and did
    /// not end its match. It is not resumed, and nothing is kept on the account: it is neither done nor skipped (#931).
    /// </summary>
    [Fact]
    public async Task A_tutorial_whose_session_could_not_go_on_is_not_resumed_and_is_kept_as_new()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock) { Journals = () => new FaultingJournal() };
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");
        await Task.WhenAny(play.Session.Outcome, Task.Delay(TimeSpan.FromSeconds(10), Cancel));

        (play.Session.IsOver, play.Session.HasEnded, play.Session.Outcome.IsFaulted).ShouldBe((true, false, true));
        play.Api.Practice!.Tutorial!.Done.ShouldBeFalse();
        var reopened = await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{Tutorial}/practice", learner);

        reopened.Status.ShouldBe(404);
        practices.Count.ShouldBe(0);
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).State.ShouldBe("new");
    }

    [Fact]
    public async Task Leaving_another_scenario_leaves_the_account_s_practice_where_it_is()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");

        var left = await api.HandleAsync("DELETE", $"{ScenariosApi.Prefix}/sc-repo-greedy-1v1-round-3/practice", learner);

        left.Status.ShouldBe(204);
        practices.ByToken(play.Token).ShouldNotBeNull();
    }

    /// <summary>Answers the three questions tutorial 1 teaches with the recorded answers, as a person following the lesson does.</summary>
    private static async Task<IReadOnlyList<Application.Matches.Decisions.PlayerDecision>> AnswerTheLessonAsync(ScenarioStore store, ScenarioPlay play)
    {
        var recorded = (await store.GetAsync(Tutorial, Cancel)).ShouldNotBeNull().Scenario.After.OfType<DecisionEntry>()
            .Where(entry => entry.Decision.Slot == Domain.Matches.PlayerSlot.Player1 && entry.Decision.Decision.Kind == PlayerOptionsKind.Action)
            .Select(entry => entry.Decision.Decision).Take(3).ToList();
        long? before = null;

        foreach (var answer in recorded)
        {
            var view = await WaitingForAsync(play, one => one.GetProperty("waitingFor").GetString() == "Action" && one.GetProperty("waitingAsked").GetInt64() != before);
            before = view.GetProperty("waitingAsked").GetInt64();
            var body = JsonSerializer.Serialize(new { asked = before, kind = "Action", creature = answer.Creature!.Value.Value, spell = answer.Spell!.ToString(), targets = answer.Targets.Select(one => one.Value).ToArray() });
            var sent = await play.Api.HandleAsync("POST", "/api/seat/player1/decision", body, play.Token);
            sent.Status.ShouldBe(204, Encoding.UTF8.GetString(sent.Body));
        }

        return recorded;
    }

    /// <summary>The scenarios' API over accounts whose game settings, and so whose tutorials done and skipped, are kept.</summary>
    private async Task<(ScenariosApi Api, AccountApi Accounts)> KeepingAsync(ScenarioStore store, ScenarioPractices practices)
    {
        var accounts = await AccountsAsync();
        accounts.Preferences = new FilePreferenceStore(_root);
        return (new ScenariosApi(store, practices, accounts), accounts);
    }

    private static (string? Opens, string? State) Standing(StudioResponse listed) =>
        (Read(listed).GetProperty("opens").GetString(), Read(listed).GetProperty("tutorials")[0].GetProperty("state").GetString());

    [Fact]
    public async Task The_first_tutorial_is_new_to_an_account_and_is_the_one_that_opens_by_itself()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);

        var listed = await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, await SignedInAsync(accounts, "learner"));

        Standing(listed).ShouldBe(($"/battle?scenario={Tutorial}", "new"));
    }

    [Fact]
    public async Task A_skipped_tutorial_never_opens_by_itself_again_for_that_account_alone()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");

        var skipped = await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/skip", learner);
        var again = await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/skip", learner);

        (skipped.Status, again.Status).ShouldBe((200, 200));
        Read(skipped).GetProperty("state").GetString().ShouldBe("skipped");
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "skipped"));
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, await SignedInAsync(accounts, "other"))).ShouldBe(($"/battle?scenario={Tutorial}", "new"));
    }

    [Fact]
    public async Task A_tutorial_is_not_kept_done_while_this_host_s_practice_of_it_is_not()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");

        var unopened = await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/done", learner);
        await OpenTutorialAsync(store, practices, "learner");
        var unfinished = await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/done", learner);

        (unopened.Status, unfinished.Status).ShouldBe((409, 409));
        Read(unfinished).GetProperty("error").GetString().ShouldBe("Tutorial.NotDone");
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).State.ShouldBe("new");
    }

    /// <summary>
    /// The first seat view that says the match is over also says the tutorial is done, in the same answer, read once,
    /// and the account can keep it done (#912). It pins the field through the route; the race of a view read before
    /// the play looks again is pinned by TutorialPlayTests, as a concession's change is looked at before this read.
    /// </summary>
    [Fact]
    public async Task A_tutorial_is_done_in_the_first_seat_view_that_says_its_match_over()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        var play = await OpenTutorialAsync(store, practices, "learner");
        (await play.Api.HandleAsync("POST", "/api/seat/player1/concede", "", play.Token)).Status.ShouldBe(200);

        var view = Read(await play.Api.HandleAsync("GET", "/api/seat/player1", "", play.Token));
        var done = await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/done", learner);

        (view.GetProperty("over").GetBoolean(), view.GetProperty("practice").GetProperty("tutorial").GetProperty("done").GetBoolean()).ShouldBe((true, true));
        done.Status.ShouldBe(200, Encoding.UTF8.GetString(done.Body));
    }

    [Fact]
    public async Task A_tutorial_played_to_its_end_is_kept_done_even_after_it_was_skipped_and_skipping_it_again_leaves_it_done()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");
        await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/skip", learner);
        var play = await OpenTutorialAsync(store, practices, "learner");
        await AnswerTheLessonAsync(store, play);
        await WaitingForAsync(play, one => one.GetProperty("practice").GetProperty("tutorial").GetProperty("done").GetBoolean());

        var done = await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/done", learner);
        var skipped = await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/skip", learner);

        done.Status.ShouldBe(200, Encoding.UTF8.GetString(done.Body));
        (Read(done).GetProperty("state").GetString(), Read(skipped).GetProperty("state").GetString()).ShouldBe(("done", "done"));
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "done"));
    }

    [Fact]
    public async Task Progress_is_kept_for_a_tutorial_alone_by_a_signed_in_account_with_a_post()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var (api, accounts) = await KeepingAsync(store, practices);
        var learner = await SignedInAsync(accounts, "learner");

        var plain = await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/sc-repo-greedy-1v1-round-3/skip", learner);
        var unknown = await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/forget", learner);
        var read = await api.HandleAsync("GET", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/skip", learner);
        var nobody = await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/skip", new AccountRequest(null, Secure: false));

        (plain.Status, unknown.Status, read.Status, nobody.Status).ShouldBe((404, 404, 404, 401));
        Read(plain).GetProperty("error").GetString().ShouldBe("Tutorial.NotFound");
        ScenariosApi.Names($"{ScenariosApi.TutorialsRoute}/{Tutorial}/skip").ShouldBeTrue();
    }

    [Fact]
    public async Task A_host_that_keeps_no_game_settings_opens_no_tutorial_by_itself_and_keeps_none_skipped()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(store, practices, accounts);
        var learner = await SignedInAsync(accounts, "learner");

        var skipped = await api.HandleAsync("POST", $"{ScenariosApi.TutorialsRoute}/{Tutorial}/skip", learner);

        skipped.Status.ShouldBe(404);
        Read(skipped).GetProperty("error").GetString().ShouldBe("Account.NoPreferences");
        Standing(await api.HandleAsync("GET", ScenariosApi.TutorialsRoute, learner)).ShouldBe((null, "new"));
    }

    [Fact]
    public async Task A_tutorial_whose_taught_answers_leave_the_script_is_done_all_the_same_and_answers_nothing_after()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var play = await OpenTutorialAsync(store, practices, "learner");

        var done = await WaitAsAsync(play, "spell:wait:v1");
        await Task.Delay(500, Cancel);
        var later = Read(await play.Api.HandleAsync("GET", "/api/seat/player1", "", play.Token));

        done.GetProperty("over").GetBoolean().ShouldBeFalse();
        done.GetProperty("board").GetProperty("roundNumber").GetInt32().ShouldBe(3);
        // The question after the lesson is still asked, unanswered, and the page draws none once it is done: the
        // recorded answers are not given past the lesson, so they never get the chance to stop fitting.
        (later.GetProperty("waitingFor").GetString(), later.GetProperty("waitingAsked").GetInt64()).ShouldBe((done.GetProperty("waitingFor").GetString(), done.GetProperty("waitingAsked").GetInt64()));
        later.GetProperty("practice").GetProperty("tutorial").GetProperty("assisted").GetArrayLength().ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Tutorial 1 as the host reads it, but with a last step at the round cap, a test's own file: its lesson runs
    /// past the hand-back, so the recorded answers stop fitting before it ends. The shipped lesson ends with its
    /// round, before they can (#682), and so cannot hand the seat back (#661).
    /// </summary>
    private ScenarioStore ShippingALessonThatOutlastsItsScript()
    {
        var directory = Path.Combine(_root, "longer-lesson");
        var tutorials = Path.Combine(directory, RepositoryScenarios.TutorialsDirectory);
        Directory.CreateDirectory(tutorials);
        var file = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "scenarios", RepositoryScenarios.TutorialsDirectory, "your-first-combat.json"))).ShouldNotBeNull();
        var cap = file["match"]!["rules"]!["roundCap"]!.GetValue<int>();
        file["tutorial"]!["steps"]!.AsArray().Add(JsonNode.Parse($$"""{ "at": { "kind": "Action", "round": {{cap}} }, "says": ["The last round."], "concepts": [] }"""));
        File.WriteAllText(Path.Combine(tutorials, "your-first-combat.json"), file.ToJsonString());

        var shipped = RepositoryScenarios.Read(directory);
        var tutorial = shipped.Get(Tutorial).ShouldNotBeNull("the test's lesson is read as tutorial 1");
        _hosted.Composer.Carry(tutorial.Document.Content, tutorial.Document.Schema!.Value);
        return new ScenarioStore(new FileArtifactStore(Path.Combine(_root, "scenarios")), _clock, shipped);
    }

    [Fact]
    public async Task A_tutorial_whose_recorded_answers_stop_fitting_before_its_end_hands_every_question_back_and_tells_the_seat_s_stream()
    {
        var store = ShippingALessonThatOutlastsItsScript();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var play = await OpenTutorialAsync(store, practices, "learner");
        var tutorial = play.Api.Practice.ShouldNotBeNull().Tutorial.ShouldNotBeNull();
        var gate = new Lock();
        // The last bump each thread made: the match may move on another thread meanwhile, and its bump is its own (#683).
        var lastBumpOn = new Dictionary<int, bool>();
        bool? toldAsItHandedBack = null;
        play.Api.Changes.Bumped += () =>
        {
            // The hand-back bumps on its own thread, outside the tutorial's lock (#658): reading it here waits for nothing.
            var handedBack = tutorial.Assisted.Count == 0;
            lock (gate)
            {
                lastBumpOn[Environment.CurrentManagedThreadId] = handedBack;
            }
        };

        // Said after the table's own handler, wired when the practice started: the stream was told by the hand-back
        // itself, on the thread that handed back, and not by the match moving.
        tutorial.GaveBack += () =>
        {
            // The match moves on another thread after the hand-back's bump and before this reads it, as it can in a full run.
            var other = new Thread(play.Api.Changes.Bump);
            other.Start();
            other.Join();
            lock (gate)
            {
                toldAsItHandedBack = lastBumpOn.GetValueOrDefault(Environment.CurrentManagedThreadId);
            }
        };

        var handedBack = await WaitAsAsync(play, "spell:wait:v1", HandedBack);

        handedBack.GetProperty("over").GetBoolean().ShouldBeFalse();
        handedBack.GetProperty("practice").GetProperty("tutorial").GetProperty("done").GetBoolean().ShouldBeFalse("the lesson runs to the round cap");
        handedBack.GetProperty("waitingFor").GetString().ShouldBeOneOf(["Evolution", "Speed", "TieOrder", "Intent", "Target"], handedBack.ToString());
        (await EventuallyAsync(() => { lock (gate) { return toldAsItHandedBack is not null; } })).ShouldBeTrue();
        lock (gate)
        {
            toldAsItHandedBack.ShouldBe(true, "the seat's stream is told when every question goes back to the person");
        }
    }

    /// <summary>Whether <paramref name="condition" /> holds within five seconds, read 50 ms apart.</summary>
    private static async Task<bool> EventuallyAsync(Func<bool> condition)
    {
        for (var waited = 0; waited < 100 && !condition(); waited++)
        {
            await Task.Delay(50, Cancel);
        }

        return condition();
    }

    [Fact]
    public async Task A_plain_scenario_s_practice_carries_no_tutorial()
    {
        var store = Shipping();
        await using var practices = new ScenarioPractices(_hosted.Composer, store, _clock);
        var accounts = await AccountsAsync();
        var opened = Read(await new ScenariosApi(store, practices, accounts).HandleAsync("POST", $"{ScenariosApi.Prefix}/sc-repo-greedy-1v1-round-3/practice", await SignedInAsync(accounts, "learner")));
        var practice = practices.ByToken(opened.GetProperty("token").GetString()).ShouldNotBeNull();

        var view = Read(await practice.Play.Api.HandleAsync("GET", "/api/seat/player1", "", practice.Play.Token));

        view.GetProperty("practice").GetProperty("tutorial").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>Whether the seat's view says the tutorial is done and asks the question after it.</summary>
    private static bool Finished(JsonElement view) => view.GetProperty("practice").GetProperty("tutorial").GetProperty("done").GetBoolean()
        && view.GetProperty("waitingFor").ValueKind == JsonValueKind.String;

    /// <summary>Whether the seat's view says no question is assisted any more: every one went back to the person.</summary>
    private static bool HandedBack(JsonElement view) => view.GetProperty("practice").GetProperty("tutorial").GetProperty("assisted").GetArrayLength() == 0;

    /// <summary>
    /// Answers every Action the person's seat is asked, each creature casting <paramref name="spell" /> on itself,
    /// until the seat's view satisfies <paramref name="until" /> (by default, <see cref="Finished" />); at most 40
    /// answers. Returns that view.
    /// </summary>
    private static async Task<JsonElement> WaitAsAsync(ScenarioPlay play, string spell, Func<JsonElement, bool>? until = null)
    {
        var reached = until ?? Finished;
        long? before = null;
        for (var answered = 0; answered < 40; answered++)
        {
            var view = await WaitingForAsync(play, one => reached(one) || (one.GetProperty("waitingFor").GetString() == "Action" && one.GetProperty("waitingAsked").GetInt64() != before));
            if (reached(view))
            {
                return view;
            }

            before = view.GetProperty("waitingAsked").GetInt64();
            var creature = view.GetProperty("waitingCreature").GetInt32();
            var sent = await play.Api.HandleAsync("POST", "/api/seat/player1/decision", JsonSerializer.Serialize(new { asked = before, kind = "Action", creature, spell, targets = new[] { creature } }), play.Token);
            sent.Status.ShouldBe(204, Encoding.UTF8.GetString(sent.Body));
        }

        var last = Read(await play.Api.HandleAsync("GET", "/api/seat/player1", "", play.Token));
        reached(last).ShouldBeTrue($"the view is not the one waited for after 40 answers: {last}");
        return last;
    }

    /// <summary>The tutorial opened as a practice by a player of this name.</summary>
    private async Task<ScenarioPlay> OpenTutorialAsync(ScenarioStore store, ScenarioPractices practices, string player)
    {
        var accounts = await AccountsAsync();
        var answer = await new ScenariosApi(store, practices, accounts).HandleAsync("POST", $"{ScenariosApi.Prefix}/{Tutorial}/practice", await SignedInAsync(accounts, player));
        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        return practices.ByToken(Read(answer).GetProperty("token").GetString()).ShouldNotBeNull().Play;
    }

    /// <summary>
    /// The seat's view once it satisfies <paramref name="until" />: the assisted questions are answered in the
    /// background, so it is read again, 50 ms apart, for up to five seconds. The last view is returned either way.
    /// </summary>
    private static async Task<JsonElement> WaitingForAsync(ScenarioPlay play, Func<JsonElement, bool> until)
    {
        var view = Read(await play.Api.HandleAsync("GET", "/api/seat/player1", "", play.Token));
        for (var waited = 0; waited < 100 && !until(view); waited++)
        {
            await Task.Delay(50, Cancel);
            view = Read(await play.Api.HandleAsync("GET", "/api/seat/player1", "", play.Token));
        }

        until(view).ShouldBeTrue(view.ToString());
        return view;
    }
}

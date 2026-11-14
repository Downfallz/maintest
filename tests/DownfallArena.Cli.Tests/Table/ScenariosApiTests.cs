using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Admin;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.Admin;
using DownfallArena.Infrastructure.Learning;
using Microsoft.Extensions.Time.Testing;

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
        var actions = new OperatorActions(new FileOperatorLog(_root), TimeProvider.System);
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

    [Fact]
    public async Task A_reloaded_page_resumes_the_practice_and_a_restart_replaces_it()
    {
        var (_, scenario) = await CutAsync();
        await using var practices = new ScenarioPractices(_hosted.Composer, _store, _clock);
        var accounts = await AccountsAsync();
        var api = new ScenariosApi(_store, practices, accounts);
        var learner = await SignedInAsync(accounts, "learner");
        var first = Read(await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", learner)).GetProperty("token").GetString();

        var resumed = Read(await api.HandleAsync("GET", $"{ScenariosApi.Prefix}/{scenario}/practice", learner)).GetProperty("token").GetString();
        var restarted = Read(await api.HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", learner)).GetProperty("token").GetString();

        resumed.ShouldBe(first);
        restarted.ShouldNotBe(first);
        practices.ByToken(first).ShouldBeNull();
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
}

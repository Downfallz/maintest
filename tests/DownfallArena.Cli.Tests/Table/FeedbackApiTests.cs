using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.FeedbackBox;
using DownfallArena.Infrastructure.Learning;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// <c>POST /api/feedback</c> (ADR 0189, docs/tabletop/feedback-plan.md, R1): a bug report or an idea from a seat or a
/// signed-in account, kept with what the host adds, behind the guards against spam.
/// </summary>
public sealed class FeedbackApiTests : IDisposable
{
    private const string Address = "203.0.113.7";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"feedback-api-{Guid.CreateVersion7():N}");
    private readonly HostedTables _hosted = new(recording: true);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly TableRegistry _registry;
    private readonly FileFeedbackStore _feedback;
    private readonly GuestAddresses _addresses = new();

    public FeedbackApiTests()
    {
        _registry = _hosted.Registry();
        _feedback = new FileFeedbackStore(Path.Combine(_root, "feedback"));
    }

    public void Dispose()
    {
        _writes.Dispose();
        _registry.Dispose();
        _hosted.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private static readonly AccountRequest SignedOut = new(null, Secure: false);

    private readonly FeedbackWrites _writes = new();

    private FeedbackApi Api(AccountApi? accounts = null, ScenarioPractices? practices = null) =>
        new(new SendFeedback(_feedback, _addresses, _writes, _clock), _registry) { Accounts = accounts, Practices = practices };

    private static string Report(bool attach = true, string happened = "The bleed did not tick after the stun.") =>
        JsonSerializer.Serialize(new
        {
            kind = "bug",
            category = "rules",
            happened,
            expected = "One damage in the upkeep.",
            attach,
            page = "https://play.example/battle?player1=secret-key",
            errors = new[] { new { message = "TypeError: x is undefined at https://play.example/battle?player1=secret-key", where = "battle.js:12" } },
        });

    private static string Idea() =>
        JsonSerializer.Serialize(new { kind = "idea", where = "lobby", problem = "I wanted to invite a friend by name and could not.", wish = "A name field." });

    private async Task<Feedback> KeptAsync(StudioResponse answer) =>
        (await _feedback.FindAsync(Read(answer).GetProperty("id").GetString()!, Cancel)).ShouldNotBeNull();

    /// <summary>A table with a person in seat 1 that has reached it, so the match is under way and waits on that person.</summary>
    private async Task<PlayedTable> BegunAsync()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        using var place = (await _registry.ReserveAsync(Cancel)).ShouldNotBeNull();
        await _registry.AddAsync(table, place, Cancel);
        await SeatViewAsync(table);
        table.Session.HasBegun.ShouldBeTrue();
        return table;
    }

    private static async Task<JsonElement> SeatViewAsync(PlayedTable table)
    {
        var seat = table.Seats[0];
        var answer = await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token);
        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        return Read(answer);
    }

    private async Task<AccountApi> AccountsAsync()
    {
        var store = new FileAccountStore(Path.Combine(_root, "accounts"));
        return new AccountApi(new AccountRegistry(store, TimeProvider.System), new AccountSessions(await store.SessionKeyAsync(Cancel), TimeProvider.System), AccountsMode.Development);
    }

    private static async Task<AccountRequest> SignedInAsync(AccountApi accounts, string name)
    {
        var signedIn = await accounts.HandleAsync("POST", "/api/account/dev", JsonSerializer.Serialize(new { username = name }), SignedOut);
        return new AccountRequest(signedIn.Headers!.Single().Value.Split(';')[0][(AccountSessions.Cookie.Length + 1)..], Secure: false);
    }

    [Fact]
    public async Task A_report_from_a_seat_is_kept_with_where_the_seat_stands_and_what_the_host_adds()
    {
        var table = await BegunAsync();
        var seat = table.Seats[0];

        var answer = await Api().HandleAsync("POST", Report(), seat.Token, SignedOut, Address, "Firefox/140");

        answer.Status.ShouldBe(201, Encoding.UTF8.GetString(answer.Body));
        var kept = await KeptAsync(answer);
        (kept.Kind, kept.Status, kept.Category, kept.Account, kept.Browser).ShouldBe((FeedbackKind.Bug, FeedbackStatus.New, "rules", null, "Firefox/140"));
        kept.Page.ShouldBe("/battle", "a page's query can carry a seat's key");
        kept.Sender.ShouldNotContain(seat.Token);
        kept.Errors.Single().ShouldBe(new PageError("TypeError: x is undefined at https://play.example/battle", "battle.js:12"), "an error quoting a seat link loses its key");
        var context = kept.Context.ShouldNotBeNull();
        (context.Table, context.Seat, context.Round).ShouldBe((table.Id, seat.Name, 1));
        context.SubPhase.ShouldNotBeNull();
        context.JournalLength.ShouldNotBeNull();
        (context.Content, context.Engine, context.Rules).ShouldBe((table.Content, Application.Learning.EngineVersion.Current.ToString(), Application.Learning.RuleSetStamp.Of(HostedTables.Rules)));
    }

    [Fact]
    public async Task The_position_attached_is_the_seat_s_own_view_and_nothing_of_the_other_seat_s()
    {
        var table = await BegunAsync();

        var kept = await KeptAsync(await Api().HandleAsync("POST", Report(), table.Seats[0].Token, SignedOut, Address, null));

        var served = (await SeatViewAsync(table)).GetProperty("board");
        var position = kept.Context!.Position!.Value;
        JsonElement.DeepEquals(position, served).ShouldBeTrue(position.GetRawText());
        position.GetProperty("slot").GetString().ShouldBe("Player1");
    }

    [Fact]
    public async Task A_report_sent_without_its_position_keeps_where_the_seat_stood_and_no_view()
    {
        var table = await BegunAsync();

        var kept = await KeptAsync(await Api().HandleAsync("POST", Report(attach: false), table.Seats[0].Token, SignedOut, Address, null));

        (kept.Context!.Table, kept.Context.Position).ShouldBe((table.Id, null));
    }

    [Fact]
    public async Task An_idea_from_a_seat_keeps_where_it_was_sent_from_and_no_position()
    {
        var table = await BegunAsync();
        var seat = table.Seats[0];

        var kept = await KeptAsync(await Api().HandleAsync("POST", Idea(), seat.Token, SignedOut, Address, null));

        var context = kept.Context.ShouldNotBeNull();
        (context.Table, context.Seat, context.Round, context.Position, context.Content).ShouldBe((table.Id, seat.Name, 1, null, null));
    }

    [Fact]
    public async Task An_idea_from_a_signed_in_account_is_kept_under_the_account()
    {
        var accounts = await AccountsAsync();
        var player = await SignedInAsync(accounts, "dreamer");

        var answer = await Api(accounts).HandleAsync("POST", Idea(), token: null, player, Address, null);

        answer.Status.ShouldBe(201, Encoding.UTF8.GetString(answer.Body));
        var kept = await KeptAsync(answer);
        (kept.Kind, kept.Category, kept.Expected, kept.Context).ShouldBe((FeedbackKind.Idea, "lobby", "A name field.", null));
        kept.Account.ShouldNotBeNull().ShouldStartWith("acct_");
        kept.Sender.ShouldBe(kept.Account);
    }

    [Fact]
    public async Task Feedback_signed_out_and_without_a_seat_is_refused()
    {
        var answer = await Api(await AccountsAsync()).HandleAsync("POST", Idea(), token: "no-such-seat", SignedOut, Address, null);

        answer.Status.ShouldBe(401);
        Read(answer).GetProperty("error").GetString().ShouldBe("Feedback.SignInRequired");
        (await _feedback.IdsAsync(Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_body_that_is_neither_kind_or_too_short_is_refused_as_invalid()
    {
        var table = await BegunAsync();

        var neither = await Api().HandleAsync("POST", """{"kind":"praise"}""", table.Seats[0].Token, SignedOut, Address, null);
        var short_ = await Api().HandleAsync("POST", Report(happened: "broken"), table.Seats[0].Token, SignedOut, Address, null);

        (neither.Status, short_.Status).ShouldBe((400, 400));
        Read(short_).GetProperty("error").GetString().ShouldBe("Feedback.Invalid");
    }

    [Fact]
    public async Task An_error_list_holding_nothing_is_read_as_no_error()
    {
        var table = await BegunAsync();
        var body = Report().Replace("\"errors\":[{", "\"errors\":[null,{", StringComparison.Ordinal);

        var answer = await Api().HandleAsync("POST", body, table.Seats[0].Token, SignedOut, Address, null);

        answer.Status.ShouldBe(201, Encoding.UTF8.GetString(answer.Body));
        (await KeptAsync(answer)).Errors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task On_a_host_where_nobody_signs_in_feedback_without_a_seat_is_told_to_come_from_a_game()
    {
        var answer = await Api().HandleAsync("POST", Idea(), token: null, SignedOut, Address, null);

        answer.Status.ShouldBe(403);
        Read(answer).GetProperty("error").GetString().ShouldBe("Feedback.SeatRequired");
    }

    [Fact]
    public async Task Feedback_sent_again_too_soon_is_refused_with_the_seconds_to_wait()
    {
        var table = await BegunAsync();
        await Api().HandleAsync("POST", Report(), table.Seats[0].Token, SignedOut, Address, null);
        _clock.Advance(TimeSpan.FromSeconds(20));

        var again = await Api().HandleAsync("POST", Report(happened: "Something else went wrong too."), table.Seats[0].Token, SignedOut, Address, null);

        again.Status.ShouldBe(429);
        Read(again).GetProperty("error").GetString().ShouldBe("Feedback.TooSoon");
        Read(again).GetProperty("retryAfterSeconds").GetInt64().ShouldBe(100);
        again.Headers!.ShouldContain(new KeyValuePair<string, string>("Retry-After", "100"));
    }

    [Fact]
    public async Task A_second_report_on_the_same_round_is_merged_into_the_first_with_its_id()
    {
        var table = await BegunAsync();
        var first = Read(await Api().HandleAsync("POST", Report(), table.Seats[0].Token, SignedOut, Address, null));
        _clock.Advance(SendFeedback.Apart);

        var second = await Api().HandleAsync("POST", Report(happened: "Tapped it again, same round."), table.Seats[0].Token, SignedOut, Address, null);

        second.Status.ShouldBe(200);
        (Read(second).GetProperty("id").GetString(), Read(second).GetProperty("code").GetString()).ShouldBe((first.GetProperty("id").GetString(), "Feedback.Merged"));
        (await _feedback.IdsAsync(Cancel)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_guest_on_a_new_seat_is_still_counted_against_its_address()
    {
        for (var sent = 0; sent < SendFeedback.PerAddress; sent++)
        {
            var table = await BegunAsync();
            (await Api().HandleAsync("POST", Report(), table.Seats[0].Token, SignedOut, Address, null)).Status.ShouldBe(201);
            _clock.Advance(SendFeedback.Apart);
        }

        var fresh = await BegunAsync();
        var refused = await Api().HandleAsync("POST", Report(), fresh.Seats[0].Token, SignedOut, Address, null);

        refused.Status.ShouldBe(429);
        Read(refused).GetProperty("error").GetString().ShouldBe("Feedback.TooMany");
    }

    [Fact]
    public async Task A_seat_that_is_another_accounts_cannot_send_for_it()
    {
        var accounts = await AccountsAsync();
        var owner = await SignedInAsync(accounts, "owner");
        var stranger = await SignedInAsync(accounts, "stranger");
        var table = await BegunAsync();
        var seat = table.Seats[0];
        (await LobbyApi.BindAsync(table, seat, (await accounts.WhoAsync(owner, Cancel))!.Account.Id)).ShouldBeTrue();

        var refused = await Api(accounts).HandleAsync("POST", Report(), seat.Token, stranger, Address, null);
        var allowed = await Api(accounts).HandleAsync("POST", Report(), seat.Token, owner, Address, null);

        (refused.Status, allowed.Status).ShouldBe((403, 201));
    }
    [Fact]
    public async Task A_report_from_a_practice_copies_its_entries_since_the_cut()
    {
        var accounts = await AccountsAsync();
        var learner = await SignedInAsync(accounts, "learner");
        var scenarios = new ScenarioStore(new FileArtifactStore(Path.Combine(_root, "scenarios")), _clock);
        await using var practices = new ScenarioPractices(_hosted.Composer, scenarios, _clock);
        var scenario = await CutAsync(scenarios);
        var opened = Read(await new ScenariosApi(scenarios, practices, accounts).HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", learner));
        var play = practices.ByToken(opened.GetProperty("token").GetString()).ShouldNotBeNull().Play;
        await AnswerOneSpeedAsync(play);

        var answer = await Api(accounts, practices).HandleAsync("POST", Report(), play.Token, learner, Address, null);

        answer.Status.ShouldBe(201, Encoding.UTF8.GetString(answer.Body));
        var context = (await KeptAsync(answer)).Context.ShouldNotBeNull();
        (context.Scenario, context.Table, context.Seat, context.Round).ShouldBe((scenario, null, "player2", 2));
        context.Position.ShouldNotBeNull();
        var entries = context.PracticeEntries.ShouldNotBeNull();
        entries.ShouldNotBeEmpty();
        entries[0].GetProperty("kind").GetString().ShouldBe("Speed");
        entries[0].GetProperty("at").GetDateTimeOffset().ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task Another_account_cannot_send_from_a_practice_it_did_not_open()
    {
        var accounts = await AccountsAsync();
        var learner = await SignedInAsync(accounts, "learner");
        var stranger = await SignedInAsync(accounts, "stranger");
        var scenarios = new ScenarioStore(new FileArtifactStore(Path.Combine(_root, "scenarios")), _clock);
        await using var practices = new ScenarioPractices(_hosted.Composer, scenarios, _clock);
        var scenario = await CutAsync(scenarios);
        var opened = Read(await new ScenariosApi(scenarios, practices, accounts).HandleAsync("POST", $"{ScenariosApi.Prefix}/{scenario}/practice", learner));

        var refused = await Api(accounts, practices).HandleAsync("POST", Report(), opened.GetProperty("token").GetString(), stranger, Address, null);

        refused.Status.ShouldBe(403);
    }

    [Fact]
    public async Task A_deleted_account_s_feedback_keeps_its_text_and_loses_the_account()
    {
        var accounts = await AccountsAsync();
        var forget = new ForgetFeedbackAccount(_feedback, _writes);
        accounts.ForgetFeedback = id => forget.HandleAsync(id.ToString());
        var player = await SignedInAsync(accounts, "dreamer");
        var id = Read(await Api(accounts).HandleAsync("POST", Idea(), token: null, player, Address, null)).GetProperty("id").GetString()!;

        (await accounts.HandleAsync("DELETE", "/api/account", """{"username":"dreamer"}""", player)).Status.ShouldBe(200);

        var kept = (await _feedback.FindAsync(id, Cancel)).ShouldNotBeNull();
        (kept.Account, kept.Sender, kept.Text).ShouldBe((null, ForgetFeedbackAccount.Gone, "I wanted to invite a friend by name and could not."));
    }

    /// <summary>A table of two bots played to its end and recorded, then cut at its second round in seat 2.</summary>
    private async Task<string> CutAsync(ScenarioStore scenarios)
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.Bots, Cancel);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), Cancel);
        for (var waited = 0; !table.IsFinished && waited < 300; waited++)
        {
            await Task.Delay(100, Cancel);
        }

        await table.RecordAsync(Cancel);
        var cut = await new ScenarioAdminApi(scenarios, _hosted.Stored!, _hosted.Composer).HandleAsync("POST", ScenarioAdminApi.Route, JsonSerializer.Serialize(new { session = table.Id, round = 2, seat = "player2" }), "operator");
        cut.Status.ShouldBe(201, Encoding.UTF8.GetString(cut.Body));
        return Read(cut).GetProperty("id").GetString()!;
    }

    /// <summary>The practice's seat answers the first speed it is asked: one entry past the cut.</summary>
    private static async Task AnswerOneSpeedAsync(ScenarioPlay play)
    {
        var view = Read(await play.Api.HandleAsync("GET", "/api/seat/player2", "", play.Token));
        var creature = view.GetProperty("options").GetProperty("speed").GetProperty("missing")[0].GetInt32();
        var sent = await play.Api.HandleAsync("POST", "/api/seat/player2/decision", JsonSerializer.Serialize(new { asked = view.GetProperty("waitingAsked").GetInt64(), kind = "Speed", creature, speed = "Quick" }), play.Token);
        sent.Status.ShouldBe(204, Encoding.UTF8.GetString(sent.Body));
    }
}

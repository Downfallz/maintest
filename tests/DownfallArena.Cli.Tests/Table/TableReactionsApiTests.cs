using System.Text;
using System.Text.Json;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The reactions two players send each other during a match (ADR 0173): one of a fixed set, a few seconds apart,
/// between two people only, read by both seats and kept with the table so a player away for a day still sees one.
/// </summary>
public sealed class TableReactionsApiTests : IDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry;

    public TableReactionsApiTests()
    {
        _registry = _hosted.Registry();
    }

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
    }

    [Fact]
    public async Task A_reaction_one_player_sends_is_in_both_players_views()
    {
        var table = await BegunAsync(HostedTables.TwoPeople);

        var sent = await ReactAsync(table, 0, "well-played");

        sent.Status.ShouldBe(200, Text(sent));
        foreach (var index in new[] { 0, 1 })
        {
            var reactions = (await SeatAsync(table, index)).GetProperty("reactions");
            reactions.GetProperty("available").GetBoolean().ShouldBeTrue();
            var recent = reactions.GetProperty("recent").EnumerateArray().Single();
            recent.GetProperty("seat").GetString().ShouldBe("player1");
            recent.GetProperty("reaction").GetString().ShouldBe("well-played");
            recent.GetProperty("sequence").GetInt64().ShouldBe(1);
        }
    }

    [Fact]
    public async Task A_reaction_tells_both_pages_the_table_changed()
    {
        var table = await BegunAsync(HostedTables.TwoPeople);
        var before = table.Api.Changes.Version;

        await ReactAsync(table, 1, "hello");

        table.Api.Changes.Version.ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task A_second_reaction_too_soon_is_refused_with_how_long_to_wait()
    {
        var table = await BegunAsync(HostedTables.TwoPeople);
        await ReactAsync(table, 0, "hello");

        var again = await ReactAsync(table, 0, "oops");

        again.Status.ShouldBe(429);
        using var body = JsonDocument.Parse(Text(again));
        body.RootElement.GetProperty("error").GetString().ShouldBe("Reaction.TooSoon");
        body.RootElement.GetProperty("retryAfterMs").GetInt64().ShouldBeInRange(1, (long)TableReactions.Cooldown.TotalMilliseconds);
    }

    [Fact]
    public async Task One_players_cooldown_does_not_hold_the_other_back()
    {
        var table = await BegunAsync(HostedTables.TwoPeople);
        await ReactAsync(table, 0, "hello");

        var answer = await ReactAsync(table, 1, "hello");

        answer.Status.ShouldBe(200, Text(answer));
    }

    [Fact]
    public async Task A_reaction_that_is_not_json_is_refused_without_the_parser_s_words()
    {
        var table = await BegunAsync(HostedTables.TwoPeople);
        var seat = table.Seats[0];

        var answer = await table.Api.HandleAsync("POST", $"/api/seat/{seat.Name}/reaction", """{ "reaction": """, seat.Token);

        answer.Status.ShouldBe(400);
        Encoding.UTF8.GetString(answer.Body).ShouldBe("The request body is not the JSON this route reads.");
    }

    [Theory]
    [InlineData("""{ "reaction": "you are bad" }""")]
    [InlineData("""{ }""")]
    public async Task Only_a_reaction_of_the_set_is_sent(string body)
    {
        var table = await BegunAsync(HostedTables.TwoPeople);
        var seat = table.Seats[0];

        var answer = await table.Api.HandleAsync("POST", $"/api/seat/{seat.Name}/reaction", body, seat.Token);

        answer.Status.ShouldBe(400);
        (await SeatAsync(table, 1)).GetProperty("reactions").GetProperty("recent").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_table_against_a_bot_offers_no_reactions()
    {
        var table = await Opened(HostedTables.OnePerson);
        var view = await SeatAsync(table, 0);

        var answer = await ReactAsync(table, 0, "good-luck");

        view.GetProperty("reactions").GetProperty("available").GetBoolean().ShouldBeFalse();
        answer.Status.ShouldBe(409);
    }

    [Fact]
    public async Task One_account_in_both_seats_is_one_person_on_a_shared_device_and_reacts_to_nobody()
    {
        var table = await Opened(HostedTables.TwoPeople);
        var account = AccountId.New();
        table.Seats[0].Account = account;
        table.Seats[1].Account = account;
        await SeatAsync(table, 0);
        var view = await SeatAsync(table, 1);

        var answer = await ReactAsync(table, 0, "hello");

        view.GetProperty("reactions").GetProperty("available").GetBoolean().ShouldBeFalse();
        answer.Status.ShouldBe(409);
    }

    [Fact]
    public async Task Nobody_reacts_before_both_players_reach_their_seats()
    {
        var table = await Opened(HostedTables.TwoPeople);
        await SeatAsync(table, 0);

        var answer = await ReactAsync(table, 0, "hello");

        answer.Status.ShouldBe(409);
    }

    [Fact]
    public async Task A_seat_cannot_react_for_the_other_one()
    {
        var table = await BegunAsync(HostedTables.TwoPeople);

        var answer = await table.Api.HandleAsync("POST", "/api/seat/player2/reaction", """{ "reaction": "oops" }""", table.Seats[0].Token);

        answer.Status.ShouldBe(403);
    }

    [Fact]
    public async Task Reactions_are_kept_when_the_host_starts_over()
    {
        var table = await BegunAsync(HostedTables.TwoPeople);
        await ReactAsync(table, 0, "thinking");
        _registry.Dispose();
        await table.QuiesceAsync(Cancel);

        using var restored = await _hosted.RestartedAsync();
        var rebuilt = (await restored.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        var recent = (await SeatAsync(rebuilt, 1)).GetProperty("reactions").GetProperty("recent").EnumerateArray().Single();

        recent.GetProperty("reaction").GetString().ShouldBe("thinking");
        restored.Dispose();
        await rebuilt.QuiesceAsync(Cancel);
    }

    [Fact]
    public async Task A_reaction_to_a_run_the_operator_deleted_is_refused_for_good_and_keeps_no_file()
    {
        var table = await BegunAsync(HostedTables.TwoPeople);
        (await ReactAsync(table, 0, "hello")).Status.ShouldBe(200);
        await table.QuiesceAsync(Cancel);
        (await _hosted.Stored!.DeleteAsync(table.Id, Cancel)).ShouldBeTrue();

        var refused = await ReactAsync(table, 1, "good-luck");

        refused.Status.ShouldBe(409, Text(refused));
        using var body = JsonDocument.Parse(Text(refused));
        body.RootElement.GetProperty("error").GetString().ShouldBe("Reaction.Gone");
        Directory.EnumerateFiles(_hosted.RunsDirectory, PlaytestRun.ReactionsFile, SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_first_reaction_to_a_run_the_operator_deleted_is_refused_and_keeps_no_file()
    {
        var table = await BegunAsync(HostedTables.TwoPeople);
        await table.QuiesceAsync(Cancel);
        (await _hosted.Stored!.DeleteAsync(table.Id, Cancel)).ShouldBeTrue();

        var refused = await ReactAsync(table, 0, "hello");

        refused.Status.ShouldBe(409, Text(refused));
        using var body = JsonDocument.Parse(Text(refused));
        body.RootElement.GetProperty("error").GetString().ShouldBe("Reaction.Gone");
        Directory.Exists(Path.Combine(_hosted.RunsDirectory, table.Run!.SessionId)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_store_that_fails_leaves_the_board_readable_and_refuses_the_reaction_with_a_code()
    {
        using var hosted = new HostedTables(directory => new RecordRefused(new FileArtifactStore(directory), PlaytestRun.ReactionsFile) { ReadsRefused = true, Refusing = true });
        using var registry = hosted.Registry();
        var table = await BegunAsync(hosted, registry, HostedTables.TwoPeople);

        var view = await SeatAsync(table, 0);
        var refused = await ReactAsync(table, 0, "hello");

        view.GetProperty("reactions").ValueKind.ShouldBe(JsonValueKind.Null);
        refused.Status.ShouldBe(503, Text(refused));
        using var body = JsonDocument.Parse(Text(refused));
        body.RootElement.GetProperty("error").GetString().ShouldBe("Reaction.Busy");
        await table.QuiesceAsync(Cancel);
    }

    [Fact]
    public async Task A_record_still_being_written_is_waited_for_before_a_first_reaction_is_judged()
    {
        RecordHeld? store = null;
        using var hosted = new HostedTables(directory => store = new RecordHeld(new FileArtifactStore(directory)));
        using var registry = hosted.Registry();
        var table = await Opened(hosted, registry, HostedTables.TwoPeople);
        store!.Hold();
        var recording = table.RecordAsync(Cancel);

        var recorded = table.Run!.RecordedAsync(Cancel);
        await Task.Delay(100, Cancel);
        var waited = !recorded.IsCompleted;
        store.Release();
        await recording;

        waited.ShouldBeTrue();
        (await recorded).ShouldBe(RecordPresence.Kept);
        await table.QuiesceAsync(Cancel);
    }

    [Fact]
    public async Task A_record_this_run_never_saw_stored_is_unknown_and_one_it_wrote_is_gone_once_deleted()
    {
        var table = await Opened(HostedTables.TwoPeople);
        var before = await table.Run!.RecordedAsync(Cancel);
        await table.RecordAsync(Cancel);
        (await _hosted.Stored!.DeleteAsync(table.Id, Cancel)).ShouldBeTrue();

        var after = await table.Run.RecordedAsync(Cancel);

        before.ShouldBe(RecordPresence.Unknown, "nothing was written yet, which is no evidence of a delete (#850)");
        after.ShouldBe(RecordPresence.Gone);
        await table.QuiesceAsync(Cancel);
    }

    [Fact]
    public async Task The_operator_s_delete_waits_for_a_reaction_on_its_way_and_leaves_nothing_of_the_run()
    {
        RecordHeld? store = null;
        using var hosted = new HostedTables(directory => store = new RecordHeld(new FileArtifactStore(directory), PlaytestRun.ReactionsFile));
        using var registry = hosted.Registry();
        var admin = new AdminApi(registry, hosted.Composer, OperatorGate.WithToken("operator"), TimeProvider.System, "Rules", hosted.Stored) { Said = _ => { } };
        var table = await BegunAsync(hosted, registry, HostedTables.TwoPeople);
        (await ReactAsync(table, 0, "hello")).Status.ShouldBe(200);
        store!.Hold();
        var reacting = ReactAsync(table, 1, "good-luck");
        await store.Reached.WaitAsync(TimeSpan.FromSeconds(30), Cancel);

        var deleting = admin.HandleAsync("DELETE", "/api/sessions", $$"""{"ids":["{{table.Id}}"]}""", "operator", null);
        await Task.Delay(200, Cancel);
        var waited = !deleting.IsCompleted;
        store.Release();
        var deleted = await deleting;
        await reacting;

        waited.ShouldBeTrue("the delete waits for the reaction on its way (#849)");
        deleted.Status.ShouldBe(200, Text(deleted));
        Directory.Exists(Path.Combine(hosted.RunsDirectory, table.Id)).ShouldBeFalse();
    }

    private Task<PlayedTable> BegunAsync(TableRequest request) => BegunAsync(_hosted, _registry, request);

    private static async Task<PlayedTable> BegunAsync(HostedTables hosted, TableRegistry registry, TableRequest request)
    {
        var table = await Opened(hosted, registry, request);
        await SeatAsync(table, 0);
        await SeatAsync(table, 1);
        table.Session.HasBegun.ShouldBeTrue();
        return table;
    }

    private Task<PlayedTable> Opened(TableRequest request) => Opened(_hosted, _registry, request);

    private static async Task<PlayedTable> Opened(HostedTables hosted, TableRegistry registry, TableRequest request)
    {
        var table = await hosted.Composer.ComposeAsync(request, Cancel);
        using var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull();
        await registry.AddAsync(table, place, Cancel);
        return table;
    }

    private static Task<StudioResponse> ReactAsync(PlayedTable table, int index, string reaction)
    {
        var seat = table.Seats[index];
        return table.Api.HandleAsync("POST", $"/api/seat/{seat.Name}/reaction", JsonSerializer.Serialize(new { reaction }), seat.Token);
    }

    private static async Task<JsonElement> SeatAsync(PlayedTable table, int index)
    {
        var seat = table.Seats[index];
        var answer = await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token);
        answer.Status.ShouldBe(200, Text(answer));
        using var document = JsonDocument.Parse(answer.Body);
        return document.RootElement.Clone();
    }

    private static string Text(StudioResponse response) => Encoding.UTF8.GetString(response.Body);
}

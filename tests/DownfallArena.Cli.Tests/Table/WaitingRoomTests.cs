using System.Text;
using System.Text.Json;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A table with people in it waits for every one of them to reach their seat before the first question, and
/// then asks both their first package pick at once (ADR 0092).
/// </summary>
public sealed class WaitingRoomTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry = new();

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
    }

    [Fact]
    public async Task A_table_for_two_people_waits_for_both_and_shows_the_first_the_code_that_brings_the_second()
    {
        var table = await Opened(new TableRequest(null, null, "mk", null, 7));
        var first = table.Seats[0].Person!;
        var second = table.Seats[1].Person!;

        var room = await Seat(table, 0);

        table.Session.HasBegun.ShouldBeFalse("one person is still to come");
        first.Waiting.ShouldBeNull("nobody is asked anything before the table is full");
        room.GetProperty("waitingFor").ValueKind.ShouldBe(JsonValueKind.Null);
        var missing = room.GetProperty("waiting").GetProperty("seats").EnumerateArray().ToList();
        missing.Count.ShouldBe(1);
        missing[0].GetProperty("slot").GetString().ShouldBe("player2");
        missing[0].GetProperty("code").GetString().ShouldBe(_registry.Codes.Of(table.Seats[1]));
        missing[0].GetProperty("join").GetString().ShouldBe($"/j/{_registry.Codes.Of(table.Seats[1])}");

        await Seat(table, 1);

        table.Session.HasBegun.ShouldBeTrue();
        (await Asked(first)).Kind.ShouldBe(PlayerOptionsKind.Evolution);
        (await Asked(second)).Kind.ShouldBe(PlayerOptionsKind.Evolution);
        (await Seat(table, 0)).GetProperty("waiting").ValueKind.ShouldBe(JsonValueKind.Null, "the room is gone once the match has begun");
    }

    /// <summary>Both are asked at once: neither pick waits for the other's, which is what picking face down means (ADR 0089).</summary>
    [Fact]
    public async Task Both_people_are_asked_their_first_package_pick_at_the_same_time()
    {
        var table = await Opened(new TableRequest(null, null, null, null, 7));
        await Seat(table, 0);
        await Seat(table, 1);
        var first = await Asked(table.Seats[0].Person!);
        var second = await Asked(table.Seats[1].Person!);

        first.Kind.ShouldBe(PlayerOptionsKind.Evolution);
        second.Kind.ShouldBe(PlayerOptionsKind.Evolution);
    }

    [Fact]
    public async Task A_table_against_a_bot_begins_the_moment_its_person_arrives_and_a_table_of_bots_begins_at_once()
    {
        var against = await Opened(HostedTables.OnePerson);
        against.Session.HasBegun.ShouldBeFalse();

        var answer = await Seat(against, 0);

        against.Session.HasBegun.ShouldBeTrue();
        answer.GetProperty("waiting").ValueKind.ShouldBe(JsonValueKind.Null, "a bot's seat needs nobody");
        (await Asked(against.Seats[0].Person!)).Kind.ShouldBe(PlayerOptionsKind.Evolution);

        var bots = await Opened(HostedTables.Bots);
        bots.Session.HasBegun.ShouldBeTrue();
    }

    [Fact]
    public async Task A_concession_while_the_table_waits_ends_the_match_rather_than_leaving_it_waiting_for_ever()
    {
        var table = await Opened(new TableRequest(null, null, null, null, 7));
        await Seat(table, 0);

        var answer = await table.Api.HandleAsync("POST", "/api/seat/player1/concede", string.Empty, table.Seats[0].Token);

        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        var outcome = await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        outcome.IsSuccess.ShouldBeTrue();
        outcome.Value.Winner.ShouldBe(PlayerSlot.Player2);

        // The session closes its files after the outcome; the store is only torn down once it has.
        for (var attempt = 0; attempt < 300 && !table.IsFinished; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        table.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task The_admin_listing_says_a_table_is_waiting_until_it_begins()
    {
        var admin = new AdminApi(_registry, _hosted.Composer, OperatorGate.WithToken("op"), TimeProvider.System, "Rules 2 creatures (a test)", _hosted.Stored);
        var opened = JsonDocument.Parse(Encoding.UTF8.GetString((await admin.HandleAsync("POST", "/api/tables", """{"player1":"person","player2":"person"}""", "op", null)).Body)).RootElement;
        var id = opened.GetProperty("id").GetString()!;
        opened.GetProperty("waiting").GetBoolean().ShouldBeTrue();

        var table = _registry.ById(id).ShouldNotBeNull();
        await Seat(table, 0);
        await Seat(table, 1);

        var listed = JsonDocument.Parse(Encoding.UTF8.GetString((await admin.HandleAsync("GET", "/api/tables", "", "op", null)).Body)).RootElement;
        listed.GetProperty("tables").EnumerateArray().Single().GetProperty("waiting").GetBoolean().ShouldBeFalse();
    }

    private async Task<PlayedTable> Opened(TableRequest request)
    {
        var table = await _hosted.Composer.ComposeAsync(request, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();
        return table;
    }

    /// <summary>A poll of the seat, which is how its person reaches it.</summary>
    private static async Task<JsonElement> Seat(PlayedTable table, int index)
    {
        var seat = table.Seats[index];
        var answer = await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token);
        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        return JsonDocument.Parse(Encoding.UTF8.GetString(answer.Body)).RootElement;
    }

    private static async Task<HumanSeat.Question> Asked(HumanSeat person)
    {
        for (var attempt = 0; attempt < 300 && person.Waiting is null; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        return person.Waiting.ShouldNotBeNull("the seat was never asked anything");
    }
}

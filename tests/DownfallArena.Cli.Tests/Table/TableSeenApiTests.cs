using System.Text;
using System.Text.Json;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// What each seat was last shown of its game, kept at the host beside the table (ADR 0194), so the recap follows a
/// player from one device to another; and when the last move was, in the seat's view.
/// </summary>
public sealed class TableSeenApiTests : IDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly HostedTables _hosted = new(directory => new FileArtifactStore(directory), turns: new NoAgentTurns());
    private readonly TableRegistry _registry;

    public TableSeenApiTests()
    {
        _registry = _hosted.Registry();
    }

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
    }

    [Fact]
    public async Task A_seat_whose_page_said_nothing_has_no_last_visit()
    {
        var table = await BegunAsync();

        var view = await SeatAsync(table, 0);

        view.GetProperty("seen").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task What_a_seat_was_shown_is_in_its_view_with_when()
    {
        var table = await BegunAsync();
        var before = DateTimeOffset.UtcNow;

        var answer = await SeenAsync(table, 0, """{ "sequence": 3 }""");

        answer.Status.ShouldBe(200, Text(answer));
        var seen = (await SeatAsync(table, 0)).GetProperty("seen");
        seen.GetProperty("sequence").GetInt64().ShouldBe(3);
        seen.GetProperty("at").GetDateTimeOffset().ShouldBeInRange(before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task A_sequence_smaller_than_the_one_kept_is_ignored_and_answered_with_the_kept_one()
    {
        var table = await BegunAsync();
        await SeenAsync(table, 0, """{ "sequence": 6 }""");

        var answer = await SeenAsync(table, 0, """{ "sequence": 2 }""");

        answer.Status.ShouldBe(200, Text(answer));
        using var body = JsonDocument.Parse(answer.Body);
        body.RootElement.GetProperty("seen").GetProperty("sequence").GetInt64().ShouldBe(6);
        (await SeatAsync(table, 0)).GetProperty("seen").GetProperty("sequence").GetInt64().ShouldBe(6);
    }

    [Fact]
    public async Task A_seat_cannot_say_what_the_other_one_was_shown()
    {
        var table = await BegunAsync();

        var answer = await table.Api.HandleAsync("PUT", "/api/seat/player2/seen", """{ "sequence": 4 }""", table.Seats[0].Token);

        answer.Status.ShouldBe(403);
        (await SeatAsync(table, 1)).GetProperty("seen").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task One_seats_last_visit_is_not_the_others()
    {
        var table = await BegunAsync();

        await SeenAsync(table, 0, """{ "sequence": 5 }""");

        (await SeatAsync(table, 1)).GetProperty("seen").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "sequence": -1 }""")]
    [InlineData("""{ "sequence": "four" }""")]
    [InlineData("not json")]
    public async Task Only_a_sequence_is_kept(string body)
    {
        var table = await BegunAsync();

        var answer = await SeenAsync(table, 0, body);

        answer.Status.ShouldBe(400);
        using var refusal = JsonDocument.Parse(answer.Body);
        refusal.RootElement.GetProperty("error").GetString().ShouldBe("Seen.Invalid");
    }

    [Fact]
    public async Task Saying_what_was_shown_moves_nothing_on_the_table()
    {
        var table = await BegunAsync();
        var before = table.Api.Changes.Version;

        await SeenAsync(table, 0, """{ "sequence": 2 }""");

        table.Api.Changes.Version.ShouldBe(before);
    }

    [Fact]
    public async Task The_view_says_when_the_last_move_was()
    {
        var table = await BegunAsync();
        await PassAsync(table, table.Seats[0]);
        var moved = (await _hosted.Composer.LastMoveAsync(table.Id, Cancel)).ShouldNotBeNull().At;

        var view = await SeatAsync(table, 1);

        view.GetProperty("lastMoveAt").GetDateTimeOffset().ShouldBe(moved);
    }

    [Fact]
    public async Task The_last_visit_is_kept_beside_the_table_and_read_by_a_host_started_over()
    {
        var table = await BegunAsync();
        await SeenAsync(table, 1, """{ "sequence": 4 }""");
        _registry.Dispose();
        await table.QuiesceAsync(Cancel);

        using var restored = await _hosted.RestartedAsync();
        var rebuilt = (await restored.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        var seen = (await SeatAsync(rebuilt, 1)).GetProperty("seen");

        seen.GetProperty("sequence").GetInt64().ShouldBe(4);
        File.Exists(Path.Combine(_hosted.RunsDirectory, table.Run!.SessionId, "seen-player2.json")).ShouldBeTrue();
        restored.Dispose();
        await rebuilt.QuiesceAsync(Cancel);
    }

    [Fact]
    public async Task A_seat_taken_by_another_player_has_none_of_the_previous_players_last_visit()
    {
        var table = await BegunAsync();
        await SeenAsync(table, 0, """{ "sequence": 9 }""");

        // A seat offered again gets a fresh key (ADR 0170): the player who takes it is another person.
        table.Seats[0].Token = "a-fresh-key-for-the-next-player";

        (await SeatAsync(table, 0)).GetProperty("seen").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_store_that_fails_leaves_the_board_readable_and_refuses_the_mark_with_a_code()
    {
        using var hosted = new HostedTables(directory => new RecordRefused(new FileArtifactStore(directory), "seen-player1.json") { ReadsRefused = true, Refusing = true }, turns: new NoAgentTurns());
        using var registry = hosted.Registry();
        var table = await BegunAsync(hosted, registry);

        var view = await SeatAsync(table, 0);
        var answer = await SeenAsync(table, 0, """{ "sequence": 2 }""");

        view.GetProperty("seen").ValueKind.ShouldBe(JsonValueKind.Null);
        answer.Status.ShouldBe(503);
        using var refusal = JsonDocument.Parse(answer.Body);
        refusal.RootElement.GetProperty("error").GetString().ShouldBe("Seen.Busy");
        await table.QuiesceAsync(Cancel);
    }

    private Task<PlayedTable> BegunAsync() => BegunAsync(_hosted, _registry);

    private static async Task<PlayedTable> BegunAsync(HostedTables hosted, TableRegistry registry)
    {
        var table = await hosted.Composer.ComposeAsync(HostedTables.TwoPeople, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        // Each person reaches their seat through the route, which is what the table's record keeps of it.
        await SeatAsync(table, 0);
        await SeatAsync(table, 1);
        foreach (var seat in table.Seats)
        {
            for (var attempt = 0; attempt < 300 && seat.Person!.Waiting is null; attempt++)
            {
                await Task.Delay(20, Cancel);
            }
        }

        return table;
    }

    private static async Task PassAsync(PlayedTable table, TableSeat seat)
    {
        var asked = seat.Person!.Waiting.ShouldNotBeNull().Asked;
        var pass = JsonSerializer.Serialize(new { kind = "Evolution", pass = true, asked });
        (await table.Api.HandleAsync("POST", $"/api/seat/{seat.Name}/decision", pass, seat.Token)).Status.ShouldBe(204);
    }

    private static Task<StudioResponse> SeenAsync(PlayedTable table, int index, string body)
    {
        var seat = table.Seats[index];
        return table.Api.HandleAsync("PUT", $"/api/seat/{seat.Name}/seen", body, seat.Token);
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

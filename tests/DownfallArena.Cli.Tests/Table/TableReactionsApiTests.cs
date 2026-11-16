using System.Text;
using System.Text.Json;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
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

    private async Task<PlayedTable> BegunAsync(TableRequest request)
    {
        var table = await Opened(request);
        await SeatAsync(table, 0);
        await SeatAsync(table, 1);
        table.Session.HasBegun.ShouldBeTrue();
        return table;
    }

    private async Task<PlayedTable> Opened(TableRequest request)
    {
        var table = await _hosted.Composer.ComposeAsync(request, Cancel);
        using var place = (await _registry.ReserveAsync(Cancel)).ShouldNotBeNull();
        await _registry.AddAsync(table, place, Cancel);
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

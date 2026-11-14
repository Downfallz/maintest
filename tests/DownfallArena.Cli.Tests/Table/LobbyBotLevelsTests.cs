using System.Text;
using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A player chooses a bot by how strong it is, Easy, Medium or Hard, and is shown that level and never the bot's name
/// (ADR 0177).
/// </summary>
public sealed class LobbyBotLevelsTests : IDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry;
    private readonly TableComposer _composer;

    public LobbyBotLevelsTests()
    {
        _registry = _hosted.Registry();
        _composer = new TableComposer(_hosted.Services, HostedTables.Rules, new FileArtifactStore(_hosted.RunsDirectory))
        {
            Index = _hosted.Index,
            Agents = [new SeatableAgent("random", "Random — the floor", Featured: true)],
        };
    }

    private LobbyApi Lobby => new(_registry, _composer, TimeProvider.System);

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
    }

    [Theory]
    [InlineData("easy", "explore:0.3")]
    [InlineData("medium", "greedy")]
    [InlineData("hard", "random")]
    [InlineData("HARD", "random")]
    public async Task Each_level_seats_its_own_bot(string level, string seated)
    {
        var opened = Read(await Lobby.HandleAsync("POST", "/api/lobby/tables", JsonSerializer.Serialize(new { opponent = "bot", level })));

        (await SeatedAsync(opened)).ShouldBe(AgentSpec.Parse(seated));
    }

    [Fact]
    public async Task A_game_against_the_bot_that_names_no_level_is_played_against_Hard()
    {
        var opened = Read(await Lobby.HandleAsync("POST", "/api/lobby/tables", """{"opponent":"bot"}"""));

        (await SeatedAsync(opened)).ShouldBe(AgentSpec.Parse("random"));
    }

    [Theory]
    [InlineData("impossible")]
    [InlineData("random")]
    public async Task A_level_the_lobby_does_not_offer_is_refused(string level)
    {
        var answer = await Lobby.HandleAsync("POST", "/api/lobby/tables", JsonSerializer.Serialize(new { opponent = "bot", level }));

        answer.Status.ShouldBe(400);
        Encoding.UTF8.GetString(answer.Body).ShouldContain("Lobby.BotLevel");
    }

    [Fact]
    public async Task A_player_is_shown_the_bots_level_and_never_its_name()
    {
        var opened = Read(await Lobby.HandleAsync("POST", "/api/lobby/tables", """{"opponent":"bot","level":"easy"}"""));

        var bot = opened.GetProperty("table").GetProperty("seats").EnumerateArray().Single(seat => seat.GetProperty("kind").GetString() == "bot");
        bot.GetProperty("name").GetString().ShouldBe("Easy");
        bot.GetProperty("level").GetString().ShouldBe("easy");
        opened.GetRawText().ShouldNotContain("explore");
    }

    [Fact]
    public async Task A_bot_no_level_seats_is_shown_to_a_player_as_a_bot()
    {
        var table = await _composer.ComposeAsync(new TableRequest(null, AgentSpec.Parse("lookahead"), "mk", null, 7), Cancel);
        using var place = (await _registry.ReserveAsync(Cancel)).ShouldNotBeNull();
        await _registry.AddAsync(table, place, Cancel);
        var token = table.Seats[0].Token;

        var resumed = Read(await Lobby.HandleAsync("POST", "/api/lobby/resume", JsonSerializer.Serialize(new { tokens = new[] { token } })));

        var bot = resumed.GetProperty("tables")[0].GetProperty("table").GetProperty("seats").EnumerateArray().Single(seat => seat.GetProperty("kind").GetString() == "bot");
        bot.GetProperty("name").GetString().ShouldBe("Bot");
        resumed.GetRawText().ShouldNotContain("ookahead");
    }

    private async Task<AgentSpec?> SeatedAsync(JsonElement opened) =>
        (await _registry.ByIdAsync(opened.GetProperty("table").GetProperty("id").GetString()!, Cancel)).ShouldNotBeNull().Request.ShouldNotBeNull().Player2;

    private static JsonElement Read(StudioResponse response)
    {
        response.Status.ShouldBeInRange(200, 201, Encoding.UTF8.GetString(response.Body));
        return JsonDocument.Parse(response.Body).RootElement.Clone();
    }
}

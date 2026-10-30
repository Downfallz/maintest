using System.Text;
using System.Text.Json;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Tests.Table;

public sealed class LobbyApiTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry = new();

    private LobbyApi Lobby => new(_registry, _hosted.Composer, TimeProvider.System);

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
    }

    [Fact]
    public async Task The_lobby_lists_public_summaries_and_returns_only_the_creators_seat()
    {
        var opened = Read(await Lobby.HandleAsync("POST", "/api/lobby/tables", """{"opponent":"person","format":"1v1"}"""));
        var id = opened.GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id).ShouldNotBeNull();
        opened.GetProperty("seats").GetArrayLength().ShouldBe(1);
        opened.GetProperty("seats")[0].GetProperty("seat").GetString().ShouldBe("player1");
        opened.GetProperty("table").GetProperty("format").GetString().ShouldBe("1v1");
        (await _hosted.Stored!.RecordAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull().OpenSeats.ShouldBe(["player2"]);

        var listed = await Lobby.HandleAsync("GET", "/api/lobby", "");
        var text = Encoding.UTF8.GetString(listed.Body);
        text.ShouldNotContain(table.Pilot.Token);
        foreach (var seat in table.Seats)
        {
            text.ShouldNotContain(seat.Token);
            text.ShouldNotContain(_registry.Codes.Of(seat)!);
        }

        Read(listed).GetProperty("tables")[0].GetProperty("openSeats").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Two_players_racing_for_one_place_cannot_both_take_it()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var answers = await Task.WhenAll(
            Lobby.HandleAsync("POST", $"/api/lobby/tables/{id}/join", "{}"),
            Lobby.HandleAsync("POST", $"/api/lobby/tables/{id}/join", "{}"));

        answers.Select(answer => answer.Status).Order().ShouldBe([200, 409]);
        (await _hosted.Stored!.RecordAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull().OpenSeats.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_invitation_consumes_the_same_place_as_the_public_join_button()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        await Lobby.SeatReachedAsync(table, table.Seats[1].Token);

        (await Lobby.HandleAsync("POST", $"/api/lobby/tables/{id}/join", "{}")).Status.ShouldBe(409);
        (await _hosted.Stored!.RecordAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull().OpenSeats.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resuming_lists_only_the_seats_proved_by_tokens_without_starting_the_game()
    {
        var opened = await Open();
        var id = opened.GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var body = JsonSerializer.Serialize(new { tokens = new[] { table.Seats[0].Token, table.Pilot.Token, "unknown" } });
        var resumed = Read(await Lobby.HandleAsync("POST", "/api/lobby/resume", body));

        resumed.GetProperty("tables").GetArrayLength().ShouldBe(1);
        resumed.GetProperty("tables")[0].GetProperty("seats").GetArrayLength().ShouldBe(1);
        table.Seats[0].Person!.HasArrived.ShouldBeFalse();
        table.Session.HasBegun.ShouldBeFalse();
    }

    [Fact]
    public async Task Leaving_a_started_game_revokes_the_old_token_and_invitation_and_a_new_player_can_take_over()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        await Lobby.HandleAsync("POST", $"/api/lobby/tables/{id}/join", "{}");
        foreach (var seat in table.Seats)
        {
            (await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", "", seat.Token)).Status.ShouldBe(200);
        }

        table.Session.HasBegun.ShouldBeTrue();
        foreach (var seat in table.Seats)
        {
            for (var attempt = 0; attempt < 300 && seat.Person!.Waiting is null; attempt++)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }

            var asked = seat.Person!.Waiting.ShouldNotBeNull().Asked;
            var pass = JsonSerializer.Serialize(new { kind = "Evolution", pass = true, asked });
            (await table.Api.HandleAsync("POST", $"/api/seat/{seat.Name}/decision", pass, seat.Token)).Status.ShouldBe(204);
        }

        var before = await table.RoundAsync();
        var token = table.Seats[1].Token;
        var code = _registry.Codes.Of(table.Seats[1])!;
        (await Lobby.HandleAsync("POST", "/api/lobby/leave", "{}", token)).Status.ShouldBe(200);

        _registry.ByToken(token).ShouldBeNull();
        _registry.Codes.Answer($"/j/{code}").Status.ShouldBe(404);
        (await table.Api.HandleAsync("GET", "/api/seat/player2", "", token)).Status.ShouldBe(403);
        (await Lobby.HandleAsync("POST", "/api/lobby/leave", "{}", token)).Status.ShouldBe(403);
        table.Session.Outcome.IsCompleted.ShouldBeFalse();

        var joined = Read(await Lobby.HandleAsync("POST", $"/api/lobby/tables/{id}/join", "{}"));
        var replacement = joined.GetProperty("seats")[0].GetProperty("token").GetString();
        replacement.ShouldNotBe(token);
        (await table.Api.HandleAsync("GET", "/api/seat/player2", "", replacement)).Status.ShouldBe(200);
        (await table.RoundAsync()).ShouldBe(before);
        (await _hosted.Stored!.RecordAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Seats[1].Token.ShouldBe(replacement);

        using var after = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory));
        using var restored = new TableRegistry();
        (await TableRestorer.RestoreAsync(after.Stored!, after.Composer, restored, TimeProvider.System, TestContext.Current.CancellationToken)).ShouldHaveSingleItem().ShouldContain("rebuilt");
        var rebuilt = restored.ById(id).ShouldNotBeNull();
        restored.ByToken(token).ShouldBeNull();
        restored.ByToken(replacement).ShouldBeSameAs(rebuilt);
        rebuilt.OpenSeats.ShouldBeEmpty();
        (await rebuilt.RoundAsync()).ShouldBe(before);
        restored.Dispose();
        await rebuilt.QuiesceAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_redeploy_keeps_claimed_places_private_and_keeps_a_vacated_place_joinable()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var oldToken = table.Seats[0].Token;
        await Lobby.HandleAsync("POST", "/api/lobby/leave", "{}", oldToken);
        using var after = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory));
        using var restored = new TableRegistry();

        var said = await TableRestorer.RestoreAsync(after.Stored!, after.Composer, restored, TimeProvider.System, TestContext.Current.CancellationToken);

        said.ShouldHaveSingleItem().ShouldContain("rebuilt");
        var rebuilt = restored.ById(id).ShouldNotBeNull();
        rebuilt.OpenSeats.ShouldBe(["player2", "player1"]);
        restored.ByToken(oldToken).ShouldBeNull();
        var lobby = new LobbyApi(restored, after.Composer, TimeProvider.System);
        Read(await lobby.HandleAsync("POST", $"/api/lobby/tables/{id}/join", "{}")).GetProperty("seats")[0].GetProperty("seat").GetString().ShouldBe("player1");
        restored.Dispose();
        await rebuilt.QuiesceAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("greedy", 1)]
    [InlineData("hotseat", 2)]
    public async Task Solo_and_shared_device_games_do_not_offer_a_public_place(string opponent, int seats)
    {
        var opened = Read(await Lobby.HandleAsync("POST", "/api/lobby/tables", JsonSerializer.Serialize(new { opponent })));
        opened.GetProperty("seats").GetArrayLength().ShouldBe(seats);
        opened.GetProperty("table").GetProperty("openSeats").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task An_invitation_only_table_is_not_made_joinable_by_appearing_in_the_lobby()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();
        (await Lobby.HandleAsync("POST", $"/api/lobby/tables/{table.Id}/join", "{}")).Status.ShouldBe(409);
    }

    [Fact]
    public async Task A_claimed_seat_stays_claimed_after_a_redeploy_before_the_player_opens_the_table()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var joined = Read(await Lobby.HandleAsync("POST", $"/api/lobby/tables/{id}/join", "{}"));
        var token = joined.GetProperty("seats")[0].GetProperty("token").GetString()!;
        using var after = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory));
        using var restored = new TableRegistry();
        await TableRestorer.RestoreAsync(after.Stored!, after.Composer, restored, TimeProvider.System, TestContext.Current.CancellationToken);
        var rebuilt = restored.ById(id).ShouldNotBeNull();
        restored.ByToken(token).ShouldBeSameAs(rebuilt);
        var lobby = new LobbyApi(restored, after.Composer, TimeProvider.System);

        (await lobby.HandleAsync("POST", $"/api/lobby/tables/{id}/join", "{}")).Status.ShouldBe(409);
        restored.Dispose();
        await rebuilt.QuiesceAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_host_that_records_nothing_refuses_to_offer_games_that_would_be_lost_on_redeploy()
    {
        using var unrecorded = new HostedTables();
        var lobby = new LobbyApi(_registry, unrecorded.Composer, TimeProvider.System);
        (await lobby.HandleAsync("POST", "/api/lobby/tables", "{}")).Status.ShouldBe(409);
        _registry.All().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/api/lobby/tables", "{broken")]
    [InlineData("/api/lobby/tables", """{"opponent":"heuristic:secret.json"}""")]
    [InlineData("/api/lobby/tables", """{"format":"invalid"}""")]
    [InlineData("/api/lobby/resume", "{}")]
    public async Task Invalid_requests_are_refused_without_opening_a_table(string path, string body)
    {
        (await Lobby.HandleAsync("POST", path, body)).Status.ShouldBe(400);
        _registry.All().ShouldBeEmpty();
    }

    private async Task<JsonElement> Open() => Read(await Lobby.HandleAsync("POST", "/api/lobby/tables", "{}"));

    private static JsonElement Read(StudioResponse response)
    {
        response.Status.ShouldBeInRange(200, 201, Encoding.UTF8.GetString(response.Body));
        return JsonDocument.Parse(response.Body).RootElement;
    }
}

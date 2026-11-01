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
            if (_registry.Codes.Of(seat) is { } code)
            {
                text.ShouldNotContain(code);
            }

            if (seat.Invitation is { } invitation)
            {
                text.ShouldNotContain(invitation);
            }
        }

        // The creator's seat is theirs from the start: no link or code names it, and only the other place is offered.
        table.Seats[0].Taken.ShouldBeTrue();
        table.Seats[0].Key.ShouldBeNull();
        _registry.Codes.Of(table.Seats[0]).ShouldBeNull();
        table.Seats[1].Invitation.ShouldNotBeNull();

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
    public async Task An_accepted_invitation_consumes_the_same_place_as_the_public_join_button()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        (await Claim(table.Seats[1].Invitation!)).Status.ShouldBe(200);

        (await Lobby.HandleAsync("POST", $"/api/lobby/tables/{id}/join", "{}")).Status.ShouldBe(409);
        (await _hosted.Stored!.RecordAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull().OpenSeats.ShouldBeEmpty();
    }

    /// <summary>A table recorded before invitations: its link carried the seat's token, and reaching the seat spends the place.</summary>
    [Fact]
    public async Task A_seat_token_from_an_older_link_still_consumes_its_open_place()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        await Lobby.SeatReachedAsync(table, table.Seats[1].Token);

        (await Lobby.HandleAsync("POST", $"/api/lobby/tables/{id}/join", "{}")).Status.ShouldBe(409);
        (await _hosted.Stored!.RecordAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull().OpenSeats.ShouldBeEmpty();
    }

    /// <summary>
    /// The case the guard exists for (ADR 0116): one link opened on two devices, or by the player who meant to
    /// pass it on. The first to accept takes the seat under a token of its own; the second finds it taken.
    /// </summary>
    [Fact]
    public async Task An_invitation_is_taken_once_whoever_opens_its_link_second()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var invitation = table.Seats[1].Invitation.ShouldNotBeNull();
        var code = _registry.Codes.Of(table.Seats[1]).ShouldNotBeNull();

        var answers = await Task.WhenAll(Claim(invitation), Claim(invitation));

        answers.Select(answer => answer.Status).Order().ShouldBe([200, 409]);
        var token = Read(answers.Single(answer => answer.Status == 200)).GetProperty("seats")[0].GetProperty("token").GetString()!;
        token.ShouldNotBe(invitation);
        table.Seats[1].Token.ShouldBe(token);
        table.Seats[1].Taken.ShouldBeTrue();
        _registry.ByInvitation(invitation).ShouldBeNull();
        _registry.Codes.Answer($"/j/{code}").ShouldNameNoSeat();
        Read(await Ask(invitation)).GetProperty("invitations")[0].GetProperty("kind").GetString().ShouldBe("spent");

        var recorded = (await _hosted.Stored!.RecordAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        recorded.OpenSeats.ShouldBeEmpty();
        recorded.Seats[1].ShouldBe(new RecordedSeat("player2", token, null, true, null, true));
    }

    /// <summary>A table recorded before invitations comes back the way its links were written: they carry the seat's token, and keep working.</summary>
    [Fact]
    public async Task A_table_recorded_before_invitations_keeps_its_links_to_the_seat_token()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();
        await table.RecordAsync(_registry.Codes, TestContext.Current.CancellationToken);
        var recorded = (await _hosted.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await table.Run!.RecordAsync(recorded with { Seats = [.. recorded.Seats.Select(seat => seat with { Invitation = null, Taken = null })] }, TestContext.Current.CancellationToken);
        var token = table.Seats[0].Token;
        _registry.Dispose();
        await table.QuiesceAsync(TestContext.Current.CancellationToken);
        using var after = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory));
        using var restored = new TableRegistry();

        await TableRestorer.RestoreAsync(after.Stored!, after.Composer, restored, TimeProvider.System, TestContext.Current.CancellationToken);

        var rebuilt = restored.ById(table.Id).ShouldNotBeNull();
        rebuilt.Seats[0].Key.ShouldBe(token);
        var code = restored.Codes.Of(rebuilt.Seats[0]).ShouldNotBeNull();
        restored.Codes.Answer($"/j/{code}").Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Location" && header.Value == $"/?player1={token}");
        restored.Dispose();
        await rebuilt.QuiesceAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A claim the store cannot save is not made: the invitation, its code and the open place are as they were, and no token was handed out.</summary>
    [Fact]
    public async Task An_invitation_whose_acceptance_cannot_be_saved_stays_outstanding()
    {
        RecordRefused? store = null;
        using var hosted = new HostedTables(directory => store = new RecordRefused(new FileArtifactStore(directory)));
        var lobby = new LobbyApi(_registry, hosted.Composer, TimeProvider.System);
        var id = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", "{}")).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var seat = table.Seats[1];
        var (token, invitation, code) = (seat.Token, seat.Invitation!, _registry.Codes.Of(seat));
        store!.Refusing = true;

        await Should.ThrowAsync<IOException>(() => lobby.HandleAsync("POST", "/api/lobby/claim", JsonSerializer.Serialize(new { tokens = new[] { invitation } })));

        (seat.Token, seat.Invitation, seat.Taken, _registry.Codes.Of(seat)).ShouldBe((token, invitation, false, code));
        table.OpenSeats.ShouldBe(["player2"]);
        _registry.ByInvitation(invitation).ShouldNotBeNull();
        store.Refusing = false;
        (await lobby.HandleAsync("POST", "/api/lobby/claim", JsonSerializer.Serialize(new { tokens = new[] { invitation } }))).Status.ShouldBe(200);
    }

    /// <summary>A table let go of while its invitation was being accepted hands out nothing.</summary>
    [Fact]
    public async Task An_invitation_to_a_table_let_go_of_is_refused()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var invitation = table.Seats[1].Invitation!;
        _registry.Remove(id).ShouldBeTrue();

        (await Claim(invitation)).Status.ShouldBe(409);
        _registry.Rekey(table, table.Seats[1], SeatKeys.TakenBy("late")).ShouldBeFalse();
        _registry.ByToken("late").ShouldBeNull();
        await table.QuiesceAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Asking_what_a_link_carries_spends_nothing()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var invitation = table.Seats[1].Invitation!;

        for (var asked = 0; asked < 2; asked++)
        {
            var answer = Read(await Ask(invitation)).GetProperty("invitations")[0];
            answer.GetProperty("kind").GetString().ShouldBe("invitation");
            answer.GetProperty("seat").GetString().ShouldBe("player2");
            answer.GetProperty("table").GetProperty("id").GetString().ShouldBe(id);
        }

        table.OpenSeats.ShouldBe(["player2"]);
        table.Seats[1].Person!.HasArrived.ShouldBeFalse();
        _registry.ByInvitation(invitation).ShouldNotBeNull();
    }

    /// <summary>An invitation is not a seat token: a page polling with it, an old one or a script, is refused, so it cannot sit down without accepting.</summary>
    [Fact]
    public async Task An_invitation_reaches_no_seat_until_it_is_accepted()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var invitation = _registry.ById(id)!.Seats[1].Invitation!;

        _registry.ByToken(invitation).ShouldBeNull();
    }

    [Fact]
    public async Task A_link_carrying_a_seat_token_is_answered_as_a_seat_and_spends_nothing()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;

        var answer = Read(await Ask(table.Seats[0].Token, table.Pilot.Token)).GetProperty("invitations");

        answer.GetArrayLength().ShouldBe(2);
        answer[0].GetProperty("kind").GetString().ShouldBe("seat");
        answer[0].GetProperty("seat").GetString().ShouldBe("player1");
        answer[1].GetProperty("kind").GetString().ShouldBe("spent", "a pilot's token is not a seat a page may sit at");
        Read(await Ask("unknown")).GetProperty("invitations")[0].GetProperty("kind").GetString().ShouldBe("spent");
        table.Seats[0].Person!.HasArrived.ShouldBeFalse();
    }

    [Fact]
    public async Task One_link_for_one_shared_browser_takes_both_seats_together()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.TwoPeople, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();

        var claimed = Read(await Claim(table.Seats[0].Invitation!, table.Seats[1].Invitation!)).GetProperty("seats");

        claimed.GetArrayLength().ShouldBe(2);
        table.Seats.ShouldAllBe(seat => seat.Taken && seat.Invitation == null);
    }

    [Fact]
    public async Task Invitations_of_two_tables_are_not_accepted_together()
    {
        var first = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        var second = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        _registry.TryAdd(first).ShouldBeTrue();
        _registry.TryAdd(second).ShouldBeTrue();

        (await Claim(first.Seats[0].Invitation!, second.Seats[0].Invitation!)).Status.ShouldBe(409);
        first.Seats[0].Taken.ShouldBeFalse();
        second.Seats[0].Taken.ShouldBeFalse();
    }

    [Fact]
    public async Task Leaving_offers_the_place_under_a_fresh_invitation()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var first = table.Seats[1].Invitation!;
        var token = Read(await Claim(first)).GetProperty("seats")[0].GetProperty("token").GetString()!;

        (await Lobby.HandleAsync("POST", "/api/lobby/leave", "{}", token)).Status.ShouldBe(200);

        var next = table.Seats[1].Invitation.ShouldNotBeNull();
        next.ShouldNotBe(first);
        table.Seats[1].Taken.ShouldBeFalse();
        _registry.Codes.Of(table.Seats[1]).ShouldNotBeNull();
        (await Claim(first)).Status.ShouldBe(409);
        (await Claim(next)).Status.ShouldBe(200);
    }

    [Fact]
    public async Task A_redeploy_keeps_an_outstanding_invitation_and_a_taken_seat_as_they_were()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var creator = table.Seats[0].Token;
        var invitation = table.Seats[1].Invitation!;
        var code = _registry.Codes.Of(table.Seats[1])!;
        using var after = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory));
        using var restored = new TableRegistry();

        await TableRestorer.RestoreAsync(after.Stored!, after.Composer, restored, TimeProvider.System, TestContext.Current.CancellationToken);

        var rebuilt = restored.ById(id).ShouldNotBeNull();
        rebuilt.Seats[0].Taken.ShouldBeTrue();
        restored.Codes.Of(rebuilt.Seats[0]).ShouldBeNull();
        restored.ByToken(creator).ShouldBeSameAs(rebuilt);
        restored.ByInvitation(invitation).ShouldNotBeNull().Seat.ShouldBeSameAs(rebuilt.Seats[1]);
        restored.Codes.Of(rebuilt.Seats[1]).ShouldBe(code);
        restored.Dispose();
        await rebuilt.QuiesceAsync(TestContext.Current.CancellationToken);
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
        _registry.Codes.Answer($"/j/{code}").ShouldNameNoSeat();
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
    [InlineData("/api/lobby/invitations", "{}")]
    [InlineData("/api/lobby/invitations", """{"tokens":["a","b","c"]}""")]
    [InlineData("/api/lobby/claim", "{}")]
    [InlineData("/api/lobby/claim", """{"tokens":[]}""")]
    public async Task Invalid_requests_are_refused_without_opening_a_table(string path, string body)
    {
        (await Lobby.HandleAsync("POST", path, body)).Status.ShouldBe(400);
        _registry.All().ShouldBeEmpty();
    }

    /// <summary>
    /// The seat's link and code are on the admin panel and in any invitation already sent. A place taken from
    /// the lobby is handed over under a fresh token and code, so whoever holds the old ones cannot share it.
    /// </summary>
    [Fact]
    public async Task Taking_a_place_from_the_lobby_revokes_the_seats_old_link_and_code()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var oldToken = table.Seats[1].Token;
        var oldCode = _registry.Codes.Of(table.Seats[1]).ShouldNotBeNull();

        var joined = Read(await Lobby.HandleAsync("POST", $"/api/lobby/tables/{id}/join", "{}"));
        var newToken = joined.GetProperty("seats")[0].GetProperty("token").GetString()!;

        newToken.ShouldNotBe(oldToken);
        _registry.ByToken(oldToken).ShouldBeNull();
        _registry.ByToken(newToken).ShouldBeSameAs(table);
        _registry.Codes.Of(table.Seats[1]).ShouldBeNull();
        _registry.Codes.Answer($"/j/{oldCode}").ShouldNameNoSeat();
        table.Seats[1].Invitation.ShouldBeNull();
        (await _hosted.Stored!.RecordAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Seats[1].Token.ShouldBe(newToken);
    }

    /// <summary>A seat write racing the end of the match must not write the record back to open: the next host would rebuild a finished game.</summary>
    [Fact]
    public async Task A_seat_write_after_the_match_ended_keeps_the_record_finished()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;

        await table.Run!.MarkAsync(TableRecord.Finished, TestContext.Current.CancellationToken);
        await table.RecordAsync(_registry.Codes, TestContext.Current.CancellationToken);

        (await _hosted.Stored!.RecordAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Finished);
    }

    [Theory]
    [InlineData("/api/lobby/tables/join")]
    [InlineData("/api/lobby/tables//join")]
    [InlineData("/api/lobby/tables/a/b/join")]
    public async Task A_join_path_without_one_table_id_is_not_a_route(string path)
    {
        (await Lobby.HandleAsync("POST", path, "{}")).Status.ShouldBe(404);
    }

    /// <summary>Where a seat's notices go (ADR 0118): kept with the seat, written down, and gone with the seat's token.</summary>
    [Fact]
    public async Task A_subscription_is_kept_for_the_seat_recorded_and_restored()
    {
        var notices = new PushNotices(new NothingSent(), "BP8", TimeProvider.System);
        var lobby = new LobbyApi(_registry, _hosted.Composer, TimeProvider.System, notices);
        var id = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", "{}")).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var token = table.Seats[0].Token;

        Read(await lobby.HandleAsync("GET", "/api/lobby/push", "")).GetProperty("publicKey").GetString().ShouldBe("BP8");
        var answer = Read(await lobby.HandleAsync("PUT", "/api/lobby/push", Browser, token));

        answer.GetProperty("notices").GetBoolean().ShouldBeTrue();
        table.Seats[0].Push.ShouldBe(new PushSubscription("https://push.example/one", BrowserKey, BrowserAuth));
        (await _hosted.Stored!.RecordAsync(id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Seats[0].Push.ShouldBe(table.Seats[0].Push);

        using var after = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory));
        using var restored = new TableRegistry();
        await TableRestorer.RestoreAsync(after.Stored!, after.Composer, restored, TimeProvider.System, TestContext.Current.CancellationToken);
        var rebuilt = restored.ById(id).ShouldNotBeNull();
        rebuilt.Seats[0].Push.ShouldBe(table.Seats[0].Push);
        restored.Dispose();
        await rebuilt.QuiesceAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_subscription_is_forgotten_on_request_and_goes_with_the_seat_when_the_player_leaves()
    {
        var lobby = new LobbyApi(_registry, _hosted.Composer, TimeProvider.System, new PushNotices(new NothingSent(), "BP8", TimeProvider.System));
        var id = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", "{}")).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var token = table.Seats[0].Token;
        Read(await lobby.HandleAsync("PUT", "/api/lobby/push", Browser, token));

        Read(await lobby.HandleAsync("DELETE", "/api/lobby/push", "", token)).GetProperty("notices").GetBoolean().ShouldBeFalse();
        table.Seats[0].Push.ShouldBeNull();

        Read(await lobby.HandleAsync("PUT", "/api/lobby/push", Browser, token));
        var second = Read(await Claim(table.Seats[1].Invitation!)).GetProperty("seats")[0].GetProperty("token").GetString()!;
        Read(await lobby.HandleAsync("PUT", "/api/lobby/push", Browser, second));
        (await lobby.HandleAsync("POST", "/api/lobby/leave", "{}", token)).Status.ShouldBe(200);

        table.Seats[0].Push.ShouldBeNull("the next player's browser is not this one");
        table.Seats[1].Push.ShouldNotBeNull("the other seat keeps its own");
    }

    [Theory]
    [InlineData("http://push.example/one", BrowserKey, BrowserAuth)]
    [InlineData("https://push.example/one", "not base64url!", BrowserAuth)]
    [InlineData("https://push.example/one", "BP8", BrowserAuth)]
    [InlineData("https://push.example/one", BrowserKey, "YWJj")]
    [InlineData("https://push.example/one", null, null)]
    [InlineData(null, null, null)]
    public async Task A_subscription_that_is_not_one_is_refused_and_nothing_is_kept(string? endpoint, string? p256dh, string? auth)
    {
        var body = JsonSerializer.Serialize(new { endpoint, keys = p256dh is null ? null : new { p256dh, auth } });
        var lobby = new LobbyApi(_registry, _hosted.Composer, TimeProvider.System, new PushNotices(new NothingSent(), "BP8", TimeProvider.System));
        var id = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", "{}")).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;

        (await lobby.HandleAsync("PUT", "/api/lobby/push", body, table.Seats[0].Token)).Status.ShouldBe(400);
        table.Seats[0].Push.ShouldBeNull();
    }

    /// <summary>The test notice a player asks for: sent now, or the push service's refusal in words, on the page.</summary>
    [Fact]
    public async Task A_test_notice_is_sent_now_and_its_outcome_said()
    {
        var sender = new Refusing();
        var lobby = new LobbyApi(_registry, _hosted.Composer, TimeProvider.System, new PushNotices(sender, "BP8", TimeProvider.System));
        var id = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", "{}")).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var token = table.Seats[0].Token;

        var unsubscribed = await lobby.HandleAsync("POST", "/api/lobby/push/test", "{}", token);
        unsubscribed.Status.ShouldBe(502);
        JsonDocument.Parse(unsubscribed.Body).RootElement.GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("no subscription");

        Read(await lobby.HandleAsync("PUT", "/api/lobby/push", Browser, token));
        var sent = Read(await lobby.HandleAsync("POST", "/api/lobby/push/test", "{}", token));
        sent.GetProperty("sent").GetBoolean().ShouldBeTrue();
        sender.Sent.ShouldHaveSingleItem().ShouldContain("\"test\":true");

        sender.Failure = new HttpRequestException("The push service answered 403: bad vapid");
        var refused = await lobby.HandleAsync("POST", "/api/lobby/push/test", "{}", token);
        refused.Status.ShouldBe(502);
        JsonDocument.Parse(refused.Body).RootElement.GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("bad vapid");
        table.Seats[0].Push.ShouldNotBeNull("a refusal is the service's, the subscription stays");

        (await lobby.HandleAsync("POST", "/api/lobby/push/test", "{}", "not-a-seat")).Status.ShouldBe(403);
        (await Lobby.HandleAsync("POST", "/api/lobby/push/test", "{}", token)).Status.ShouldBe(404, "this host has no keys");
    }

    private sealed class Refusing : IPushSender
    {
        public List<string> Sent { get; } = [];

        public Exception? Failure { get; set; }

        public Task<bool> SendAsync(PushSubscription to, string payload, CancellationToken cancellationToken = default)
        {
            if (Failure is { } failure)
            {
                return Task.FromException<bool>(failure);
            }

            Sent.Add(payload);
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task A_subscription_needs_a_seat_token_and_a_host_with_keys()
    {
        var id = (await Open()).GetProperty("table").GetProperty("id").GetString()!;
        var token = _registry.ById(id)!.Seats[0].Token;

        (await Lobby.HandleAsync("PUT", "/api/lobby/push", Browser, token)).Status.ShouldBe(404, "this host has no keys");
        Read(await Lobby.HandleAsync("GET", "/api/lobby/push", "")).GetProperty("publicKey").ValueKind.ShouldBe(JsonValueKind.Null);
        var lobby = new LobbyApi(_registry, _hosted.Composer, TimeProvider.System, new PushNotices(new NothingSent(), "BP8", TimeProvider.System));
        (await lobby.HandleAsync("PUT", "/api/lobby/push", Browser, "not-a-seat")).Status.ShouldBe(403);
        (await lobby.HandleAsync("PUT", "/api/lobby/push", Browser, null)).Status.ShouldBe(403);
    }

    /// <summary>A real P-256 point and sixteen bytes of secret, as the browser would serialise them: the host refuses anything shorter.</summary>
    private const string BrowserKey = "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A";

    private const string BrowserAuth = "AAECAwQFBgcICQoLDA0ODw";

    private static readonly string Browser = JsonSerializer.Serialize(new { endpoint = "https://push.example/one", expirationTime = (object?)null, keys = new { p256dh = BrowserKey, auth = BrowserAuth } });

    /// <summary>The race ADR 0118 guards: a subscription sent for a seat whose token was just replaced must not reach the next player's seat.</summary>
    [Fact]
    public async Task A_subscription_sent_with_a_token_that_was_replaced_is_refused_and_kept_by_nobody()
    {
        var lobby = new LobbyApi(_registry, _hosted.Composer, TimeProvider.System, new PushNotices(new NothingSent(), "BP8", TimeProvider.System));
        var id = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", "{}")).GetProperty("table").GetProperty("id").GetString()!;
        var table = _registry.ById(id)!;
        var departed = table.Seats[0].Token;
        (await lobby.HandleAsync("POST", "/api/lobby/leave", "{}", departed)).Status.ShouldBe(200);

        (await lobby.HandleAsync("PUT", "/api/lobby/push", Browser, departed)).Status.ShouldBe(403);

        table.Seats[0].Push.ShouldBeNull();
    }

    private sealed class NothingSent : IPushSender
    {
        public Task<bool> SendAsync(PushSubscription to, string payload, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private async Task<JsonElement> Open() => Read(await Lobby.HandleAsync("POST", "/api/lobby/tables", "{}"));

    private Task<StudioResponse> Claim(params string[] tokens) => Lobby.HandleAsync("POST", "/api/lobby/claim", JsonSerializer.Serialize(new { tokens }));

    private Task<StudioResponse> Ask(params string[] tokens) => Lobby.HandleAsync("POST", "/api/lobby/invitations", JsonSerializer.Serialize(new { tokens }));

    private static JsonElement Read(StudioResponse response)
    {
        response.Status.ShouldBeInRange(200, 201, Encoding.UTF8.GetString(response.Body));
        return JsonDocument.Parse(response.Body).RootElement;
    }
}

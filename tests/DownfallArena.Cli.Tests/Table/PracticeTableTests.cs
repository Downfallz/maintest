using System.Text;
using System.Text.Json.Nodes;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Cli.Tests.Studio;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Tests.Table;

public sealed class PracticeTableTests : IDisposable
{
    private readonly StudioContent _scratch = new();

    public void Dispose() => _scratch.Dispose();

    private CliOptions Options()
    {
        new ContentStore(Path.Combine(AppContext.BaseDirectory, "practice-content")).Build(Path.Combine(_scratch.Path, "built"));
        return new CliOptions { Command = "table", Practice = true, Output = "unused", SchemaPath = Path.Combine(_scratch.Path, "built", "game.schema.json") };
    }

    private static JsonNode Json(StudioResponse response) => JsonNode.Parse(Encoding.UTF8.GetString(response.Body))!;

    [Theory]
    [InlineData("targeting", 1, "Target", "spell:lightning_bolt:v1")]
    [InlineData("multiclass", 3, "Evolution", null)]
    [InlineData("stun", 5, "Target", "spell:tranquilizer_dart:v1")]
    [InlineData("resolution", 5, "Target", "spell:toxic_waves:v1")]
    public async Task Recipes_reach_the_promised_question_through_the_real_engine(string scenario, int round, string question, string? spell)
    {
        await using var table = new PracticeTable(Options(), "practice-token");
        var start = await table.HandleAsync("POST", "/api/practice", $$"""{"scenario":"{{scenario}}"}""", "practice-token");
        start.Status.ShouldBe(200, Encoding.UTF8.GetString(start.Body));
        var state = Json(start);
        var seat = await table.HandleAsync("GET", "/api/seat/player1", "", state["seatToken"]!.GetValue<string>());
        var view = Json(seat);
        view["board"]!["roundNumber"]!.GetValue<int>().ShouldBe(round);
        view["waitingFor"]!.GetValue<string>().ShouldBe(question);
        view["recording"]!.GetValue<bool>().ShouldBeFalse();
        view["playedByBot"]!.GetValue<bool>().ShouldBeFalse();
        view["feed"]!.AsArray().ShouldBeEmpty("setup rounds must not reopen a replay over the starting question");
        if (spell is not null)
        {
            view["options"]!["target"]!["spell"]!.GetValue<string>().ShouldBe(spell);
            view["guidance"]!.AsArray().Count.ShouldBeGreaterThan(0);
            view["waitingCreature"]!.GetValue<int>().ShouldBe(1);
        }
        else
        {
            var actor = view["board"]!["allies"]![0]!;
            actor["acquiredTiers"]!.ToJsonString().ShouldContain("tier:brute:v1");
            var offered = view["options"]!["evolution"]!["creatures"]![0]!["availableTiers"]!.ToJsonString();
            offered.ShouldContain("tier:occultist:v1");
            offered.ShouldContain("tier:berserker:v1");
        }
    }

    /// <summary>
    /// The recipes at the slot set up the boards the free-activation playtest asks about
    /// (docs/tabletop/free-activation-playtest.md): each is checked for the one thing it exists to show.
    /// </summary>
    [Fact]
    public async Task A_recipe_at_the_slot_plays_spells_chosen_at_the_slot()
    {
        var (table, view, token) = await Started("slot-armor");
        await using var _ = table;

        view["waitingFor"]!.GetValue<string>().ShouldBe("Action");
        var catalogue = Json(await table.HandleAsync("GET", "/api/catalogue", "", token));
        catalogue["rules"]!["combatDeclaration"]!.GetValue<string>().ShouldBe("AtActivation");
    }

    [Fact]
    public async Task The_armor_recipe_asks_the_person_after_the_enemy_has_cast_Carapace()
    {
        var (table, view, _) = await Started("slot-armor");
        await using var _ = table;

        view["board"]!["enemies"]![0]!["totalDefense"]!.GetValue<int>().ShouldBe(3);
        Revealed(view).ShouldContain("spell:full_plate:v1");
        view["waitingCreature"]!.GetValue<int>().ShouldBe(view["board"]!["allies"]![0]!["id"]!.GetValue<int>());
    }

    [Fact]
    public async Task The_overdrive_recipe_makes_Fury_payable_before_the_person_chooses()
    {
        var (table, view, _) = await Started("slot-overdrive");
        await using var _ = table;

        Revealed(view).ShouldContain("spell:adrenaline_tonic:v1");
        view["board"]!["allies"]![0]!["energy"]!.GetValue<int>().ShouldBe(4);
        Offered(view).ShouldContain("spell:enraged_charge:v1");
    }

    [Fact]
    public async Task The_drain_recipe_takes_Fury_out_of_reach_before_the_person_chooses()
    {
        var (table, view, _) = await Started("slot-drain");
        await using var _ = table;

        Revealed(view).ShouldContain("spell:extort:v1");
        view["board"]!["allies"]![0]!["energy"]!.GetValue<int>().ShouldBe(2);
        Offered(view).ShouldNotContain("spell:enraged_charge:v1");
        Offered(view).ShouldContain("spell:reckless_swing:v1");
    }

    [Fact]
    public async Task The_stun_recipe_skips_the_stunned_creature_and_asks_the_next()
    {
        var (table, view, _) = await Started("slot-stun");
        await using var _ = table;

        var allies = view["board"]!["allies"]!;
        allies[0]!["isStunned"]!.GetValue<bool>().ShouldBeTrue();
        view["waitingCreature"]!.GetValue<int>().ShouldBe(allies[1]!["id"]!.GetValue<int>());
    }

    [Fact]
    public async Task The_chain_recipe_hands_the_person_their_three_allies_acting_first_in_round_five()
    {
        var (table, view, _) = await Started("slot-chain");
        await using var _ = table;

        view["board"]!["roundNumber"]!.GetValue<int>().ShouldBe(5);
        var slots = view["board"]!["timeline"]!.AsArray();
        slots.Take(3).ShouldAllBe(slot => slot!["owner"]!.GetValue<string>() == "Player1");
        view["waitingCreature"]!.GetValue<int>().ShouldBe(slots[0]!["creature"]!.GetValue<int>());
    }

    [Fact]
    public async Task The_chain_recipe_opens_on_the_ally_that_can_cast_Contagion()
    {
        var (table, view, _) = await Started("slot-chain");
        await using var _ = table;

        Offered(view).ShouldContain("spell:infectious_blast:v1");
    }

    /// <summary>A started recipe and the person's first view of it; the caller disposes the table.</summary>
    private async Task<(PracticeTable Table, JsonNode View, string Token)> Started(string scenario)
    {
        var table = new PracticeTable(Options(), "practice-token");
        var start = await table.HandleAsync("POST", "/api/practice", $$"""{"scenario":"{{scenario}}"}""", "practice-token");
        start.Status.ShouldBe(200, Encoding.UTF8.GetString(start.Body));
        var token = Json(start)["seatToken"]!.GetValue<string>();
        var view = Json(await table.HandleAsync("GET", "/api/seat/player1", "", token));
        return (table, view, token);
    }

    private static List<string> Revealed(JsonNode view) =>
        [.. view["board"]!["revealedActions"]!.AsArray().Select(action => action!["spell"]!.GetValue<string>())];

    private static List<string> Offered(JsonNode view) =>
        [.. view["options"]!["action"]!["spells"]!.AsArray().Select(option => option!["spell"]!.GetValue<string>())];

    [Fact]
    public async Task Restart_reproduces_the_board_but_revokes_the_old_seat_and_its_pending_decision()
    {
        await using var table = new PracticeTable(Options(), "practice-token");
        var first = Json(await table.HandleAsync("POST", "/api/practice", """{"scenario":"stun"}""", "practice-token"));
        var old = first["seatToken"]!.GetValue<string>();
        var before = Json(await table.HandleAsync("GET", "/api/seat/player1", "", old));
        var second = Json(await table.HandleAsync("POST", "/api/practice", """{"scenario":"stun"}""", "practice-token"));
        var fresh = second["seatToken"]!.GetValue<string>();
        fresh.ShouldNotBe(old);
        (await table.HandleAsync("GET", "/api/seat/player1", "", old)).Status.ShouldBe(403);
        (await table.HandleAsync("POST", "/api/seat/player1/decision", """{"kind":"Target","targets":[4],"asked":1}""", old)).Status.ShouldBe(403);
        var after = Json(await table.HandleAsync("GET", "/api/seat/player1", "", fresh));
        foreach (var field in new[] { "allies", "enemies", "timeline", "revealedActions" })
        {
            after["board"]![field]!.ToJsonString().ShouldBe(before["board"]![field]!.ToJsonString());
        }
    }

    [Fact]
    public async Task Only_the_practice_capability_can_start_or_reset_a_scenario()
    {
        await using var table = new PracticeTable(Options(), "practice-token");
        (await table.HandleAsync("GET", "/api/practice", "", null)).Status.ShouldBe(403);
        (await table.HandleAsync("POST", "/api/practice", "{}", "unknown")).Status.ShouldBe(403);
        (await table.HandleAsync("GET", "/api/seat/player1", "", "practice-token")).Status.ShouldBe(409);
        var state = Json(await table.HandleAsync("GET", "/api/practice", "", "practice-token"));
        state["scenarios"]!.AsArray().Count.ShouldBe(9);
        state["active"].ShouldBeNull();
        var started = Json(await table.HandleAsync("POST", "/api/practice", """{"scenario":"targeting"}""", "practice-token"));
        var seat = started["seatToken"]!.GetValue<string>();
        (await table.HandleAsync("POST", "/api/practice", """{"scenario":"stun"}""", seat)).Status.ShouldBe(403);
        (await table.HandleAsync("GET", "/api/seat/player2", "", seat)).Status.ShouldBe(403);
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("[]")]
    [InlineData("{\"scenario\":null}")]
    [InlineData("{\"scenario\":\"missing\"}")]
    public async Task An_invalid_recipe_is_refused_without_replacing_the_active_session(string body)
    {
        await using var table = new PracticeTable(Options(), "practice-token");
        var first = Json(await table.HandleAsync("POST", "/api/practice", """{"scenario":"targeting"}""", "practice-token"));
        (await table.HandleAsync("POST", "/api/practice", body, "practice-token")).Status.ShouldBe(400);
        var after = Json(await table.HandleAsync("GET", "/api/practice", "", "practice-token"));
        after["seatToken"]!.GetValue<string>().ShouldBe(first["seatToken"]!.GetValue<string>());
        (await table.HandleAsync("DELETE", "/api/practice", "", "practice-token")).Status.ShouldBe(404);
    }

    [Fact]
    public async Task Incompatible_content_fails_clearly_instead_of_hanging_at_an_unreachable_question()
    {
        new ContentStore(_scratch.Path).Build(Path.Combine(_scratch.Path, "dst"));
        var options = new CliOptions { Command = "table", Output = "unused", SchemaPath = Path.Combine(_scratch.Path, "dst", "game.schema.json") };
        await using var table = new PracticeTable(options, "practice-token");
        var result = await table.HandleAsync("POST", "/api/practice", """{"scenario":"targeting"}""", "practice-token");
        result.Status.ShouldBe(409);
        Encoding.UTF8.GetString(result.Body).ShouldContain("Rebuild the standard catalogue");
    }

    [Fact]
    public void Practice_never_silently_overrides_a_recording_seed_rules_or_seat_choice()
    {
        var options = CliOptions.Parse(["table", "--practice"]);
        options.Practice.ShouldBeTrue();
        options.Recording.ShouldBeFalse();
        foreach (var argument in new[] { "--record", "--trace", "--rules", "--seed", "--handover", "--p1", "--p2" })
        {
            Should.Throw<ArgumentException>(() => CliOptions.Parse(["table", "--practice", argument, "1"]));
        }

        Should.Throw<ArgumentException>(() => CliOptions.Parse(["play", "--practice"]));
    }

    [Theory]
    [InlineData("targeting")]
    [InlineData("multiclass")]
    [InlineData("stun")]
    [InlineData("resolution")]
    [InlineData("slot-armor")]
    [InlineData("slot-overdrive")]
    [InlineData("slot-drain")]
    [InlineData("slot-stun")]
    [InlineData("slot-chain")]
    public async Task A_person_can_continue_every_recipe_to_a_real_outcome(string scenario)
    {
        await using var table = new PracticeTable(Options(), "practice-token");
        var started = Json(await table.HandleAsync("POST", "/api/practice", $$"""{"scenario":"{{scenario}}"}""", "practice-token"));
        var token = started["seatToken"]!.GetValue<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var seen = new HashSet<long>();
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            var view = Json(await table.HandleAsync("GET", "/api/seat/player1", "", token));
            if (view["over"]!.GetValue<bool>())
            {
                var session = Json(await table.HandleAsync("GET", "/api/session", "", token));
                session["outcome"].ShouldNotBeNull();
                break;
            }

            if (view["waitingAsked"] is not { } asked || !seen.Add(asked.GetValue<long>()))
            {
                await Task.Delay(1, timeout.Token);
                continue;
            }

            var reply = Answer(view);
            reply["asked"] = asked.DeepClone();
            var result = await table.HandleAsync("POST", "/api/seat/player1/decision", reply.ToJsonString(), token);
            result.Status.ShouldBe(204, Encoding.UTF8.GetString(result.Body));
        }
    }

    private static JsonObject Answer(JsonNode view)
    {
        var kind = view["waitingFor"]!.GetValue<string>();
        var options = view["options"]!;
        var actor = view["waitingCreature"];
        var reply = new JsonObject { ["kind"] = kind, ["creature"] = actor?.DeepClone() };
        switch (kind)
        {
            case "Evolution":
                reply["pass"] = true;
                break;
            case "Speed":
                reply["speed"] = "Standard";
                break;
            case "TieOrder":
                reply["order"] = options["tieOrder"]!["asRolled"]!.DeepClone();
                break;
            case "Intent":
                reply["spell"] = options["intent"]!["creatures"]!.AsArray()
                    .First(one => one!["creature"]!.GetValue<int>() == actor!.GetValue<int>())!["castableSpells"]![0]!.DeepClone();
                break;
            case "Target":
                var legal = options["target"]!["legalTargets"]!;
                reply["targets"] = new JsonArray([.. legal["candidates"]!.AsArray().Take(legal["maxTargets"]!.GetValue<int>()).Select(one => one!.DeepClone())]);
                break;
            case "Action":
                var first = options["action"]!["spells"]![0]!;
                var targets = first["legalTargets"]!;
                reply["spell"] = first["spell"]!.DeepClone();
                reply["targets"] = new JsonArray([.. targets["candidates"]!.AsArray().Take(targets["maxTargets"]!.GetValue<int>()).Select(one => one!.DeepClone())]);
                break;
            default:
                throw new InvalidOperationException($"Unexpected question {kind}.");
        }

        return reply;
    }
}

using System.Text;
using System.Text.Json.Nodes;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Matches.Scenarios;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// <c>table --scenario</c> (docs/tabletop/scenarios-plan.md, stage S3): the practice page on one scenario file, opened
/// at the top of its round in the seat it names, on the content the file carries, and nothing recorded.
/// </summary>
public sealed class ScenarioTableTests
{
    private static readonly string Kept = Path.Combine(AppContext.BaseDirectory, "scenarios", "greedy-1v1-round-3.json");

    private static JsonNode Json(StudioResponse response) => JsonNode.Parse(Encoding.UTF8.GetString(response.Body))!;

    private static async Task<ScenarioTable> OpenedAsync(ScenarioDocument document)
    {
        // No content of the host's own: the scenario plays on the content it carries.
        var options = new CliOptions { Command = "table", Output = "unused", Scenario = Kept, SchemaPath = Path.Combine(Path.GetTempPath(), "no-content-here", "game.schema.json") };
        var table = new ScenarioTable(options, await ScenarioContent.SchemaOfAsync(document, options), document, "practice-token");
        await table.StartAsync();
        return table;
    }

    [Fact]
    public async Task The_seat_opens_at_the_top_of_the_scenario_s_round_with_nothing_replayed_or_recorded()
    {
        var document = ScenarioFile.Read(await File.ReadAllTextAsync(Kept, TestContext.Current.CancellationToken));
        await using var table = await OpenedAsync(document);

        var state = Json(await table.HandleAsync("GET", "/api/practice", "", "practice-token"));
        var view = Json(await table.HandleAsync("GET", $"/api/seat/{state["seat"]}", "", state["seatToken"]!.GetValue<string>()));

        state["seat"]!.GetValue<string>().ShouldBe("player1");
        state["recording"]!.GetValue<bool>().ShouldBeFalse();
        state["scenarios"]![0]!["name"]!.GetValue<string>().ShouldBe(document.Title);
        view["board"]!["roundNumber"]!.GetValue<int>().ShouldBe(document.Scenario.Cut.Round);
        view["recording"]!.GetValue<bool>().ShouldBeFalse();
        view["playedByBot"]!.GetValue<bool>().ShouldBeFalse();
        view["feed"]!.AsArray().ShouldBeEmpty("the rounds before the cut are not replayed over the first question");
    }

    [Fact]
    public async Task The_seat_the_person_does_not_play_is_not_theirs_to_read()
    {
        var document = ScenarioFile.Read(await File.ReadAllTextAsync(Kept, TestContext.Current.CancellationToken));
        await using var table = await OpenedAsync(document);
        var token = Json(await table.HandleAsync("GET", "/api/practice", "", "practice-token"))["seatToken"]!.GetValue<string>();

        var other = await table.HandleAsync("GET", "/api/seat/player2", "", token);

        other.Status.ShouldBe(403);
    }

    [Fact]
    public async Task A_restart_starts_the_scenario_again_from_its_cut_under_a_new_seat()
    {
        var document = ScenarioFile.Read(await File.ReadAllTextAsync(Kept, TestContext.Current.CancellationToken));
        await using var table = await OpenedAsync(document);
        var first = Json(await table.HandleAsync("GET", "/api/practice", "", "practice-token"))["seatToken"]!.GetValue<string>();

        var restarted = Json(await table.HandleAsync("POST", "/api/practice", "{}", "practice-token"));

        var second = restarted["seatToken"]!.GetValue<string>();
        second.ShouldNotBe(first);
        Json(await table.HandleAsync("GET", "/api/seat/player1", "", second))["board"]!["roundNumber"]!.GetValue<int>().ShouldBe(document.Scenario.Cut.Round);
    }

    [Fact]
    public async Task The_practice_route_answers_only_the_link_the_host_printed()
    {
        var document = ScenarioFile.Read(await File.ReadAllTextAsync(Kept, TestContext.Current.CancellationToken));
        await using var table = await OpenedAsync(document);

        (await table.HandleAsync("GET", "/api/practice", "", "someone-else")).Status.ShouldBe(403);
    }

    /// <summary>
    /// A seat that changed hands at the source's table before the cut (the operator handing a bot's seat to a person)
    /// is the source's seating, not the scenario's: the scenario still opens on its own person and scripted opponent.
    /// </summary>
    [Fact]
    public async Task A_seat_that_changed_hands_before_the_cut_does_not_reseat_the_scenario()
    {
        var kept = ScenarioFile.Read(await File.ReadAllTextAsync(Kept, TestContext.Current.CancellationToken));
        var scenario = kept.Scenario;
        var changed = kept with
        {
            Scenario = scenario with
            {
                Before = [new SeatChangeEntry(PlayerSlot.Player2, "greedy", 2), .. scenario.Before],
                Cut = scenario.Cut with { CutAt = scenario.Cut.CutAt + 1 },
            },
        };
        await using var table = await OpenedAsync(changed);

        var state = Json(await table.HandleAsync("GET", "/api/practice", "", "practice-token"));
        var view = Json(await table.HandleAsync("GET", "/api/seat/player1", "", state["seatToken"]!.GetValue<string>()));

        view["board"]!["roundNumber"]!.GetValue<int>().ShouldBe(scenario.Cut.Round);
        view["playedByBot"]!.GetValue<bool>().ShouldBeFalse();
    }
}

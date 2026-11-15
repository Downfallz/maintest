using DownfallArena.Cli.Table;
using DownfallArena.Cli.Tests.Studio;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The scenarios the repository ships (docs/tabletop/scenarios-plan.md, S7): every file of <c>scenarios/</c> read
/// under an id made of its name, and <c>scenario-generate</c>, which writes one from a match of two bots.
/// </summary>
public sealed class RepositoryScenariosTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"repository-scenarios-{Guid.CreateVersion7():N}");

    private static string Tutorial => Path.Combine(AppContext.BaseDirectory, "scenarios", "tutorials", "your-first-combat.json");

    public RepositoryScenariosTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Every_scenario_file_of_the_directory_is_read_under_an_id_made_of_its_name()
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "scenarios", "greedy-1v1-round-3.json"), Path.Combine(_root, "Greedy 1v1_Round.3.json"));

        var shipped = RepositoryScenarios.Read(_root);

        var one = shipped.All.ShouldHaveSingleItem();
        (one.Id, one.File).ShouldBe(("sc-repo-greedy-1v1-round-3", "Greedy 1v1_Round.3.json"));
        ScenarioStore.Names(one.Id).ShouldBeTrue();
        shipped.Get(one.Id).ShouldBe(one);
        shipped.Get("sc-repo-another").ShouldBeNull();
    }

    [Fact]
    public void A_file_that_is_not_a_scenario_is_left_out_and_the_others_are_kept()
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "scenarios", "greedy-1v1-round-3.json"), Path.Combine(_root, "kept.json"));
        File.WriteAllText(Path.Combine(_root, "broken.json"), "{ not json");
        File.WriteAllText(Path.Combine(_root, "other.json"), """{"format":"downfall.scenario.v9"}""");
        File.WriteAllText(Path.Combine(_root, "README.md"), "Not a scenario.");
        var kept = File.ReadAllText(Path.Combine(_root, "kept.json"));
        File.WriteAllText(Path.Combine(_root, "no-match.json"), kept.Replace("01a1d80e-539c-7556-be91-e03eb2bec54f", "not-a-guid", StringComparison.Ordinal));
        var bare = System.Text.Json.Nodes.JsonNode.Parse(kept)!.AsObject();
        bare.Remove("schema").ShouldBeTrue();
        File.WriteAllText(Path.Combine(_root, "no-content.json"), bare.ToJsonString());

        RepositoryScenarios.Read(_root).All.Select(one => one.Id).ShouldBe(["sc-repo-kept"]);
    }

    [Fact]
    public void Of_two_files_whose_names_make_the_same_id_the_first_is_kept()
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "scenarios", "greedy-1v1-round-3.json"), Path.Combine(_root, "a b.json"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "scenarios", "greedy-1v1-round-3.json"), Path.Combine(_root, "a-b.json"));

        RepositoryScenarios.Read(_root).All.Select(one => (one.Id, one.File)).ShouldBe([("sc-repo-a-b", "a b.json")]);
    }

    [Fact]
    public void A_tutorial_is_read_from_the_tutorials_directory_under_an_id_that_says_so()
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "scenarios", "greedy-1v1-round-3.json"), Path.Combine(_root, "plain.json"));
        Directory.CreateDirectory(Path.Combine(_root, "tutorials"));
        File.Copy(Tutorial, Path.Combine(_root, "tutorials", "your-first-combat.json"));

        var shipped = RepositoryScenarios.Read(_root);

        shipped.All.Select(one => (one.Id, one.File)).ShouldBe([("sc-repo-plain", "plain.json"), ("sc-repo-tutorial-your-first-combat", "tutorials/your-first-combat.json")]);
        shipped.Tutorials.ShouldHaveSingleItem().Document.Tutorial!.Order.ShouldBe(1);
        ScenarioStore.Names(shipped.Tutorials[0].Id).ShouldBeTrue();
    }

    [Fact]
    public void Tutorials_are_listed_in_the_order_of_the_series_and_a_place_taken_twice_keeps_the_first_file()
    {
        var first = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Tutorial))!.AsObject();
        Directory.CreateDirectory(Path.Combine(_root, "tutorials"));
        File.WriteAllText(Path.Combine(_root, "tutorials", "b-first.json"), first.ToJsonString());
        File.WriteAllText(Path.Combine(_root, "tutorials", "c-first-again.json"), first.ToJsonString());
        first["tutorial"]!["order"] = 2;
        File.WriteAllText(Path.Combine(_root, "tutorials", "a-second.json"), first.ToJsonString());

        RepositoryScenarios.Read(_root).Tutorials.Select(one => one.File).ShouldBe(["tutorials/b-first.json", "tutorials/a-second.json"]);
    }

    [Fact]
    public void A_tutorial_whose_lesson_is_not_one_is_left_out()
    {
        var broken = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Tutorial))!.AsObject();
        broken["tutorial"]!["steps"]![0]!["concepts"] = new System.Text.Json.Nodes.JsonArray("luck");
        Directory.CreateDirectory(Path.Combine(_root, "tutorials"));
        File.WriteAllText(Path.Combine(_root, "tutorials", "broken.json"), broken.ToJsonString());

        RepositoryScenarios.Read(_root).All.ShouldBeEmpty();
    }

    [Fact]
    public async Task Checking_a_tutorial_file_checks_that_its_steps_are_reached()
    {
        var late = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(Tutorial, TestContext.Current.CancellationToken))!.AsObject();
        late["tutorial"]!["steps"]![2]!["at"]!["round"] = 99;
        var never = Path.Combine(_root, "never.json");
        await File.WriteAllTextAsync(never, late.ToJsonString(), TestContext.Current.CancellationToken);

        var held = await ScenarioCommands.CheckAsync(new CliOptions { Command = CliOptions.ScenarioCheckCommand, Output = "unused", Scenario = Tutorial, SchemaPath = Path.Combine(_root, "none.json") });
        var refused = await ScenarioCommands.CheckAsync(new CliOptions { Command = CliOptions.ScenarioCheckCommand, Output = "unused", Scenario = never, SchemaPath = Path.Combine(_root, "none.json") });

        (held, refused).ShouldBe((0, 1));
    }

    [Fact]
    public void A_generated_scenario_s_own_file_tells_its_bots_seed_and_round_apart()
    {
        var weights = Application.Agents.AgentSpec.Parse("heuristic:learning/weights/mixture-worst.json");

        ScenarioCommands.DefaultFile(Application.Agents.AgentSpec.Greedy, Application.Agents.AgentSpec.Greedy, 5, 3).ShouldBe("greedy-v-greedy-seed-5-round-3.json");
        ScenarioCommands.DefaultFile(Application.Agents.AgentSpec.Greedy, Application.Agents.AgentSpec.Greedy, -5, 3).ShouldBe("greedy-v-greedy-seed-m5-round-3.json");
        ScenarioCommands.DefaultFile(weights, Application.Agents.AgentSpec.Random, 9, 12).ShouldBe("heuristic-learning-weights-mix-seed-9-round-12.json");
        ScenarioStore.Names(RepositoryScenarios.IdOf(ScenarioCommands.DefaultFile(weights, weights, int.MinValue, 20))).ShouldBeTrue();
    }

    [Fact]
    public void A_directory_that_is_not_there_ships_nothing() =>
        RepositoryScenarios.Read(Path.Combine(_root, "none")).All.ShouldBeEmpty();

    [Fact]
    public void A_long_file_name_still_makes_an_id_a_scenario_s_routes_take() =>
        ScenarioStore.Names(RepositoryScenarios.IdOf($"{new string('a', 90)}.json")).ShouldBeTrue();

    [Fact]
    public async Task A_generated_scenario_is_a_match_of_two_bots_cut_at_a_round_that_carries_its_content_and_holds()
    {
        using var content = new StudioContent();
        new ContentStore(content.Path).Build(Path.Combine(content.Path, "dst"));
        var schema = Path.Combine(content.Path, "dst", "game.schema.json");
        var output = Path.Combine(_root, "generated.json");
        var options = CliOptions.Parse(["scenario-generate", "--schema", schema, "--seed", "5", "--format", "1v1", "--round", "2", "--seat", "player2", "--goal", "Try it", "--out", output]);

        var written = await ScenarioCommands.GenerateAsync(options);
        var held = await ScenarioCommands.CheckAsync(new CliOptions { Command = CliOptions.ScenarioCheckCommand, Output = "unused", Scenario = output, SchemaPath = Path.Combine(_root, "no-content-here.json") });

        (written, held).ShouldBe((0, 0));
        var document = ScenarioFile.Read(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken));
        (document.Scenario.Cut.Round, document.Seat, document.Title, document.Goal, document.Session).ShouldBe((2, Domain.Matches.PlayerSlot.Player2, "Greedy against Greedy, seed 5", "Try it", null));
        document.Schema.ShouldNotBeNull();
        RepositoryScenarios.Read(_root).All.ShouldHaveSingleItem().Id.ShouldBe("sc-repo-generated");
    }

    [Fact]
    public async Task A_round_the_generated_match_does_not_reach_is_refused()
    {
        using var content = new StudioContent();
        new ContentStore(content.Path).Build(Path.Combine(content.Path, "dst"));
        var output = Path.Combine(_root, "never.json");
        var options = CliOptions.Parse(["scenario-generate", "--schema", Path.Combine(content.Path, "dst", "game.schema.json"), "--format", "1v1", "--round", "99", "--out", output]);

        (await ScenarioCommands.GenerateAsync(options)).ShouldBe(1);
        File.Exists(output).ShouldBeFalse();
    }
}

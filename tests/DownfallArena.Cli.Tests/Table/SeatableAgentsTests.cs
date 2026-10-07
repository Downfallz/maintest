using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>The bots a page offers come from the weights directory, so a new search is offered without a page change.</summary>
public sealed class SeatableAgentsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"downfall-weights-{Guid.NewGuid():N}");

    public SeatableAgentsTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        File.Delete(Featured);
    }

    [Fact]
    public void The_featured_file_comes_first_in_its_order_then_the_built_ins_then_every_other_weights_file()
    {
        File.WriteAllText(Path.Combine(_directory, "search-19.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "kill-first.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "search-2.json"), "{}");
        File.WriteAllText(Featured, $$"""
            {"featured":[
              {"agent":"heuristic:{{Spec("search-19.json")}}","label":"search-19 — the strongest"},
              {"agent":"lookahead:{{Spec("search-19.json")}}","label":"Lookahead on search-19"},
              {"agent":"greedy","label":"Greedy — the baseline"}
            ]}
            """);

        var offered = SeatableAgents.Read(_directory, Featured);

        offered.Select(agent => agent.Value).ShouldBe([
            $"heuristic:{Spec("search-19.json")}",
            $"lookahead:{Spec("search-19.json")}",
            "greedy",
            "random",
            $"heuristic:{Spec("kill-first.json")}",
            $"heuristic:{Spec("search-2.json")}",
        ]);
        offered[0].Label.ShouldBe("search-19 — the strongest");
        offered[0].Featured.ShouldBeTrue();
        offered[3].Featured.ShouldBeFalse();
        offered[4].Label.ShouldBe("kill-first");
    }

    [Fact]
    public void Without_a_featured_file_the_built_ins_and_the_files_are_still_offered()
    {
        File.WriteAllText(Path.Combine(_directory, "stun-first.json"), "{}");

        var offered = SeatableAgents.Read(_directory, Path.Combine(_directory, "..", "nowhere.json"));

        offered.Select(agent => agent.Value).ShouldBe(["greedy", "random", $"heuristic:{Spec("stun-first.json")}"]);
    }

    /// <summary>A set searched for the lookahead lives in a folder of its own: offered as a heuristic it plays another reading.</summary>
    [Fact]
    public void A_weights_file_in_a_folder_of_its_own_is_not_offered_as_a_heuristic()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "lookahead"));
        File.WriteAllText(Path.Combine(_directory, "lookahead", "lookahead-34.json"), "{}");

        var offered = SeatableAgents.Read(_directory, featuredFile: null);

        offered.Select(agent => agent.Value).ShouldBe(["greedy", "random"]);
    }

    [Fact]
    public void A_featured_file_that_does_not_parse_puts_nothing_forward_and_stops_nothing()
    {
        File.WriteAllText(Featured, "{not json");

        SeatableAgents.Read(_directory, Featured).Select(agent => agent.Value).ShouldBe(["greedy", "random"]);
    }

    [Fact]
    public void A_directory_that_is_not_there_offers_the_built_ins()
    {
        SeatableAgents.Read(Path.Combine(_directory, "nowhere"), featuredFile: null).Select(agent => agent.Value).ShouldBe(["greedy", "random"]);
    }

    /// <summary>
    /// The repository's own file names weights that exist, so what the panel puts forward can be seated. A spec
    /// names its file after the kind and whatever sits between them (<c>lookahead:4x4:&lt;file&gt;</c>,
    /// <c>explore:0.2:heuristic:&lt;file&gt;</c>), so the file is the segment that reads as one.
    /// </summary>
    [Fact]
    public void The_repository_s_featured_agents_name_files_that_exist()
    {
        var weights = Path.Combine(RepositoryRoot(), "learning", "weights");
        var offered = SeatableAgents.Read(weights, Path.Combine(RepositoryRoot(), SeatableAgents.FeaturedFile)).Where(agent => agent.Featured).ToList();

        offered.ShouldNotBeEmpty();
        foreach (var path in offered.SelectMany(agent => agent.Value.Split(':')).Where(part => part.EndsWith(".json", StringComparison.Ordinal)))
        {
            File.Exists(Path.Combine(RepositoryRoot(), path)).ShouldBeTrue($"{path} is put forward but is not in the repository");
        }
    }

    private string Featured => Path.Combine(_directory, "..", $"seatable-{Path.GetFileName(_directory)}.json");

    private string Spec(string file) => $"{_directory.Replace('\\', '/')}/{file}";

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DownfallArena.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found above the test binaries.");
    }
}

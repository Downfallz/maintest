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

    /// <summary>The repository's own file names weights that exist, so what the panel puts forward can be seated.</summary>
    [Fact]
    public void The_repository_s_featured_agents_name_files_that_exist()
    {
        var weights = Path.Combine(RepositoryRoot(), "learning", "weights");
        var offered = SeatableAgents.Read(weights, Path.Combine(RepositoryRoot(), SeatableAgents.FeaturedFile)).Where(agent => agent.Featured).ToList();

        offered.ShouldNotBeEmpty();
        foreach (var path in offered.Select(agent => agent.Value.Split(':', 2)).Where(parts => parts.Length == 2).Select(parts => parts[1]))
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

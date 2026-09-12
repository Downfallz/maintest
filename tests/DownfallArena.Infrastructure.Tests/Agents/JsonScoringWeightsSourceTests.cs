using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Infrastructure.Agents;
using DownfallArena.Infrastructure.Tests.Resources;

namespace DownfallArena.Infrastructure.Tests.Agents;

public sealed class JsonScoringWeightsSourceTests
{
    [Fact]
    public void The_committed_greedy_weights_are_the_built_in_ones()
    {
        new JsonScoringWeightsSource().Load(Path.Combine(AppContext.BaseDirectory, "weights", "greedy.json")).ShouldBe(ScoringWeights.Default);
    }

    /// <summary>
    /// A searched set arrives as a file a workflow wrote (`.github/workflows/search.yml`), so the thing that
    /// can go wrong is a file the engine refuses to read. Every committed set is loaded here rather than named,
    /// so the next one is covered the day it lands.
    /// </summary>
    [Fact]
    public void Every_committed_weights_file_loads()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "weights");
        var files = Directory.GetFiles(directory, "*.json");
        var source = new JsonScoringWeightsSource();

        files.ShouldNotBeEmpty();
        foreach (var file in files)
        {
            source.Load(file).ShouldNotBeNull($"'{Path.GetFileName(file)}' is not a weights file the engine can read.");
        }
    }

    [Fact]
    public void A_partial_file_keeps_the_built_in_values_for_the_rest()
    {
        using var directory = new ContentDirectory().WithFile("partial.json", """{ "damage": 2.5, "kill": 0 }""");

        new JsonScoringWeightsSource().Load(Path.Combine(directory.Path, "partial.json")).ShouldBe(ScoringWeights.Default with { Damage = 2.5, Kill = 0 });
    }

    [Fact]
    public void Unknown_weights_and_bad_files_are_refused()
    {
        using var directory = new ContentDirectory()
            .WithFile("unknown.json", """{ "damage": 1, "luck": 3 }""")
            .WithFile("empty.json", "null");
        var source = new JsonScoringWeightsSource();

        Should.Throw<JsonException>(() => source.Load(Path.Combine(directory.Path, "unknown.json")));
        Should.Throw<InvalidDataException>(() => source.Load(Path.Combine(directory.Path, "empty.json")));
        Should.Throw<FileNotFoundException>(() => source.Load(Path.Combine(directory.Path, "missing.json")));
        Should.Throw<ArgumentException>(() => source.Load(" "));
    }
}

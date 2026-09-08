using System.Text.Json;
using DownfallArena.Application;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Messaging;
using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.Infrastructure.Tests.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Infrastructure.Tests.Learning;

/// <summary>
/// The samples under <c>viewer/samples</c> are what the viewer is tried with, so their shape must be the shape
/// the engine records: every key path of a real run must exist in the samples, and every non-polymorphic key
/// path of the samples must exist in a real run. Polymorphic parts (events, effects, outcomes) are compared
/// per kind, since a short real run does not produce every kind.
/// </summary>
public sealed class ViewerSamplesTests
{
    private static readonly string SamplesDirectory = Path.Combine(AppContext.BaseDirectory, "samples");
    private static readonly string SampleRun = Path.Combine(SamplesDirectory, "random-vs-random");

    [Fact]
    public async Task The_committed_samples_have_the_shape_of_a_recorded_run()
    {
        using var scratch = new ContentDirectory();
        var run = Path.Combine(scratch.Path, "run");
        await RecordAsync(run, TestContext.Current.CancellationToken);

        AssertSameShape(ShapesOf(Path.Combine(SampleRun, RunRecorder.ManifestFile)), ShapesOf(Path.Combine(run, RunRecorder.ManifestFile)), "manifest");
        AssertSameShape(ShapesOfLines(Path.Combine(SampleRun, RunRecorder.StepsFile)), ShapesOfLines(Path.Combine(run, RunRecorder.StepsFile)), "steps");
        AssertSameShape(ShapesOfLines(Path.Combine(SampleRun, RunRecorder.EpisodesFile)), ShapesOfLines(Path.Combine(run, RunRecorder.EpisodesFile)), "episodes");

        var sampleTrace = ShapesOf(Directory.GetFiles(Path.Combine(SampleRun, RunRecorder.TracesDirectory)).Single());
        var realTraces = Directory.GetFiles(Path.Combine(run, RunRecorder.TracesDirectory)).SelectMany(ShapesOf).ToHashSet(StringComparer.Ordinal);
        AssertSameShape(sampleTrace, realTraces, "traces");
        AssertSameShape(ShapesOf(Path.Combine(SamplesDirectory, "evaluation.json")), ShapesOf(Path.Combine(run, "evaluation.json")), "evaluation");
    }

    /// <summary>
    /// Every key path the engine writes must be in the sample, and every sample key path outside a kind-specific
    /// part must be written by the engine. Kind-specific parts (a Pass step, a Stun outcome) may be missing from
    /// a short real run, but when a kind is present on both sides its keys are compared by the first rule.
    /// </summary>
    private static void AssertSameShape(HashSet<string> sample, HashSet<string> real, string what)
    {
        real.ShouldNotBeEmpty(what);
        real.Where(path => !sample.Contains(path)).ShouldBeEmpty($"{what}: the engine records key paths the sample does not have");
        sample.Where(path => !path.Contains('<', StringComparison.Ordinal) && !real.Contains(path)).ShouldBeEmpty($"{what}: the sample has key paths the engine does not record");
    }

    [Fact]
    public void The_csv_sample_has_the_header_of_the_batch_csv()
    {
        File.ReadLines(Path.Combine(SamplesDirectory, "simulation.csv")).First().ShouldBe(BatchResultCsv.Header);
    }

    [Fact]
    public void The_training_sample_has_the_fields_the_viewer_reads()
    {
        var first = JsonDocument.Parse(File.ReadLines(Path.Combine(SamplesDirectory, "training.jsonl")).First());

        foreach (var field in new[] { "iteration", "loss", "winRate", "winRateLow", "winRateHigh", "matches", "stamp" })
        {
            first.RootElement.TryGetProperty(field, out _).ShouldBeTrue(field);
        }
    }

    private static async Task RecordAsync(string runDirectory, CancellationToken cancellationToken)
    {
        using var content = new ContentDirectory().WithValidContent();
        GameSchemaBuilder.Write(GameSchemaBuilder.Build(content.Path), content.Output);
        var resources = GameSchemaBuilder.Load(Path.Combine(content.Output, GameSchemaJson.SchemaFileName));
        var rules = RuleSet.Create(2, 2, 2, 5, 2.0);
        var schema = FeatureSchema.Build(resources, rules);

        var services = new ServiceCollection().AddApplication().AddInfrastructure(randomSeed: 1);
        services.AddSingleton<IGameResources>(resources);
        services.AddSingleton<MatchTraceRecorder>();
        services.AddSingleton<IDomainEventListener>(provider => provider.GetRequiredService<MatchTraceRecorder>());
        services.AddSingleton<IDomainEventListener>(provider => provider.GetRequiredService<CombatStatsRecorder>());
        await using var provider = services.BuildServiceProvider();

        var recorder = new RunRecorder(
            new FileArtifactWriter(runDirectory),
            RunStamp.Create(new EngineVersion("abc123def456", false), resources, rules, schema, "Random", "Random", 1),
            new ObservationBuilder(schema, resources),
            new ActionEncoder(schema),
            TimeProvider.System,
            provider.GetRequiredService<MatchTraceRecorder>());
        var roster = Enumerable.Repeat(resources.Creatures.First().Id, rules.TeamSize).ToList();
        var scenario = new SimulationScenario { RuleSet = rules, Player1Roster = roster, Player2Roster = roster, Matches = 3, BaseSeed = 1 };

        await recorder.StartAsync(cancellationToken);
        await provider.GetRequiredService<BatchRunner>().RunAsync(scenario, recorder, cancellationToken);
        await recorder.FinishAsync(cancellationToken);

        var evaluation = await provider.GetRequiredService<EvaluationRunner>().RunAsync(
            new EvaluationScenario { RuleSet = rules, Roster = roster, AgentA = AgentSpec.Random, AgentB = AgentSpec.Random, Seeds = [1, 2] },
            recorder.Stamp,
            cancellationToken);
        await new FileArtifactWriter(runDirectory).WriteJsonAsync("evaluation.json", evaluation, cancellationToken);
    }

    private static HashSet<string> ShapesOf(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var shapes = new HashSet<string>(StringComparer.Ordinal);
        Collect(document.RootElement, string.Empty, shapes);
        return shapes;
    }

    private static HashSet<string> ShapesOfLines(string path)
    {
        var shapes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path).Where(line => line.Length > 0))
        {
            using var document = JsonDocument.Parse(line);
            Collect(document.RootElement, string.Empty, shapes);
        }

        return shapes;
    }

    /// <summary>
    /// Every key path of a JSON value: objects contribute their property names (prefixed by their kind when they
    /// carry one), arrays "[]", and dictionaries (keyed by numbers or by content ids) "*".
    /// </summary>
    private static bool IsMapKey(string name) => name.All(char.IsAsciiDigit) || name.Contains(':', StringComparison.Ordinal);

    private static void Collect(JsonElement element, string prefix, HashSet<string> shapes)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Collect(item, prefix + "[]", shapes);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var kind = element.TryGetProperty("kind", out var value) && value.ValueKind == JsonValueKind.String ? $"<{value.GetString()}>" : string.Empty;
        var properties = element.EnumerateObject().ToList();
        var dictionary = properties.Count > 0 && properties.TrueForAll(property => IsMapKey(property.Name));
        foreach (var property in properties)
        {
            var path = $"{prefix}{kind}.{(dictionary ? "*" : property.Name)}";
            shapes.Add(path);
            Collect(property.Value, path, shapes);
        }
    }
}

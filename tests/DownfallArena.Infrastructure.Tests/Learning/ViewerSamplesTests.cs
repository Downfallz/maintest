using System.Text.Json;
using DownfallArena.Application;
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

        ShapesOf(Path.Combine(SampleRun, RunRecorder.ManifestFile)).ShouldBe(ShapesOf(Path.Combine(run, RunRecorder.ManifestFile)));
        ShapesOfLines(Path.Combine(SampleRun, RunRecorder.StepsFile)).ShouldBe(ShapesOfLines(Path.Combine(run, RunRecorder.StepsFile)));
        ShapesOfLines(Path.Combine(SampleRun, RunRecorder.EpisodesFile)).ShouldBe(ShapesOfLines(Path.Combine(run, RunRecorder.EpisodesFile)));

        var sampleTrace = ShapesOf(Directory.GetFiles(Path.Combine(SampleRun, RunRecorder.TracesDirectory)).Single());
        var realTraces = Directory.GetFiles(Path.Combine(run, RunRecorder.TracesDirectory)).SelectMany(ShapesOf).ToHashSet(StringComparer.Ordinal);
        realTraces.ShouldNotBeEmpty();
        realTraces.Where(path => !sampleTrace.Contains(path)).ShouldBeEmpty("the engine records key paths the sample trace does not have");
        sampleTrace.Where(path => !path.Contains('<', StringComparison.Ordinal) && !realTraces.Contains(path)).ShouldBeEmpty("the sample trace has key paths the engine does not record");
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
    /// carry one), arrays "[]", and objects keyed by numbers (dictionaries) "*".
    /// </summary>
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
        var dictionary = properties.Count > 0 && properties.TrueForAll(property => property.Name.All(char.IsAsciiDigit));
        foreach (var property in properties)
        {
            var path = $"{prefix}{kind}.{(dictionary ? "*" : property.Name)}";
            shapes.Add(path);
            Collect(property.Value, path, shapes);
        }
    }
}

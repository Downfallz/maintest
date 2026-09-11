using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Evaluation;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Tests.Studio;

/// <summary>
/// What <c>studio --export</c> writes for a page served without the engine behind it (ADR 0023): the three
/// read-only payloads, as files, saying the same thing the routes say.
/// </summary>
public sealed class StudioExportTests : IDisposable
{
    private readonly StudioContent _content = new();
    private readonly StudioApi _api;
    private readonly string _output;

    public StudioExportTests()
    {
        var options = new CliOptions { Command = "studio", Output = "out.csv", SchemaPath = Path.Combine(_content.Path, "dst", "game.schema.json") };
        _api = new StudioApi(
            new ContentStore(_content.Path),
            new StudioRunner(options, Path.Combine(_content.Path, "runs"), TimeProvider.System),
            new BenchmarkStore(Path.Combine(_content.Path, "benchmarks")),
            RuleSet.Default,
            Path.Combine(_content.Path, "dst"));
        _output = Path.Combine(_content.Path, "site", "data");
    }

    public void Dispose()
    {
        _api.Dispose();
        _content.Dispose();
    }

    [Fact]
    public async Task The_export_writes_the_three_files_the_hosted_page_reads()
    {
        (await StudioExport.WriteAsync(_api, _output, "data")).ShouldBe(0);

        File.Exists(Path.Combine(_output, StudioExport.CatalogueFile)).ShouldBeTrue();
        File.Exists(Path.Combine(_output, StudioExport.AuditFile)).ShouldBeTrue();
        File.Exists(Path.Combine(_output, StudioExport.WeightsFile)).ShouldBeTrue();
    }

    [Fact]
    public async Task The_published_catalogue_says_what_the_route_says()
    {
        await StudioExport.WriteAsync(_api, _output, "data");

        var published = Read(StudioExport.CatalogueFile);
        var route = JsonSerializer.SerializeToElement(_api.Catalogue(), StudioJson.FileOptions);

        published.GetProperty("spells").GetArrayLength().ShouldBe(route.GetProperty("spells").GetArrayLength());
        published.GetProperty("contentHash").GetString().ShouldBe(route.GetProperty("contentHash").GetString());
        published.GetProperty("aliases").EnumerateObject().Count().ShouldBe(route.GetProperty("aliases").EnumerateObject().Count());
    }

    /// <summary>
    /// The one thing a published catalogue must not say. <see cref="ContentStore.Root"/> is the absolute path of
    /// the machine that read it, which on a runner is a path nobody can use and that a public site has no
    /// business carrying; the export writes the directory as it was asked for instead.
    /// </summary>
    [Fact]
    public async Task The_published_catalogue_carries_the_directory_it_was_asked_for_not_the_one_it_resolved()
    {
        await StudioExport.WriteAsync(_api, _output, "data");

        Read(StudioExport.CatalogueFile).GetProperty("directory").GetString().ShouldBe("data");
        _api.Catalogue().Directory.ShouldBe(_content.Path);
    }

    [Fact]
    public async Task The_published_weights_are_the_engine_weights_under_their_own_names()
    {
        await StudioExport.WriteAsync(_api, _output, "data");

        var values = Read(StudioExport.WeightsFile).GetProperty("values");
        foreach (var (name, value) in ScoringWeights.Default.Named)
        {
            values.GetProperty(name).GetDouble().ShouldBe(value);
        }
    }

    [Fact]
    public async Task The_published_audit_carries_the_findings_and_whether_a_digest_exists()
    {
        await StudioExport.WriteAsync(_api, _output, "data");

        var audit = Read(StudioExport.AuditFile);
        audit.TryGetProperty("audit", out _).ShouldBeTrue();
        audit.GetProperty("benchmark").GetProperty("exists").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task The_export_creates_the_directory_it_is_given()
    {
        var nested = Path.Combine(_output, "deeper", "still");

        await StudioExport.WriteAsync(_api, nested, "data");

        File.Exists(Path.Combine(nested, StudioExport.CatalogueFile)).ShouldBeTrue();
    }

    private JsonElement Read(string name) =>
        JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(_output, name)));
    /// <summary>
    /// The knobs ride in the catalogue rather than in a file of their own: the page reads them off
    /// <c>catalogue.balance</c>, so one payload keeps the content and what may be tuned about it in step. A
    /// published page that got one without the other would draw bands against numbers it did not have.
    /// </summary>
    [Fact]
    public async Task The_published_catalogue_carries_the_balance_knobs()
    {
        _content.Write("balance/knobs.json", """
            { "version": "knobs:v1", "spells": { "spell:strike": { "intent": "The floor.", "knobs": [] } } }
            """);

        (await StudioExport.WriteAsync(_api, _output, "data")).ShouldBe(0);

        var balance = Read(StudioExport.CatalogueFile).GetProperty("balance");
        balance.GetProperty("version").GetString().ShouldBe("knobs:v1");
        balance.GetProperty("spells").GetProperty("spell:strike").GetProperty("intent").GetString().ShouldBe("The floor.");
    }

    [Fact]
    public async Task A_catalogue_published_without_knobs_says_so_rather_than_omitting_the_field()
    {
        (await StudioExport.WriteAsync(_api, _output, "data")).ShouldBe(0);

        Read(StudioExport.CatalogueFile).GetProperty("balance").ValueKind.ShouldBe(JsonValueKind.Null);
    }

}

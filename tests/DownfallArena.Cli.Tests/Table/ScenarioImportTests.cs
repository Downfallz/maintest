using System.IO.Compression;
using System.Text.Json.Nodes;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Cli.Table;
using DownfallArena.Cli.Tests.Studio;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// <c>scenario-import</c> and <c>scenario-check --trace</c> (docs/tabletop/feedback-plan.md, R6): the zip a bug's issue
/// carries becomes a file of <c>scenarios/bugs/</c> only when it is redacted and rebuilds, and a trace prints the boards
/// after each entry past the cut.
/// </summary>
public sealed class ScenarioImportTests : IDisposable
{
    private const string Report = "20261117T101500123Z-ab12cd";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"scenario-import-{Guid.CreateVersion7():N}");

    private readonly StudioContent _content = new();

    public ScenarioImportTests()
    {
        Directory.CreateDirectory(_root);
        new ContentStore(_content.Path).Build(Path.Combine(_content.Path, "dst"));
    }

    private string Schema => Path.Combine(_content.Path, "dst", "game.schema.json");

    public void Dispose()
    {
        _content.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task A_redacted_scenario_from_an_issue_is_written_where_it_was_named_and_listed_as_its_bug()
    {
        var zip = await ZipAsync(await RedactedAsync());
        var output = Path.Combine(_root, "shipped", "bugs", "812-a-bleed.json");

        var imported = await ScenarioCommands.ImportAsync(Import(zip, output));

        imported.ShouldBe(0);
        var bug = RepositoryScenarios.Read(Path.Combine(_root, "shipped")).All.ShouldHaveSingleItem();
        (bug.Id, bug.Bug, bug.Document.Session).ShouldBe(("sc-repo-bug-812-a-bleed", 812, Report));
    }

    [Fact]
    public async Task A_bug_file_is_named_after_its_issue_and_the_operator_s_title_or_its_round()
    {
        var document = ScenarioFile.Read(await RedactedAsync());

        ScenarioCommands.BugFile(812, document).ShouldBe("812-round-2.json");
        ScenarioCommands.BugFile(9, document with { Title = null }).ShouldBe("9-round-2.json");
        ScenarioCommands.BugFile(812, document with { Title = "A bleed that does not tick after a stun ends, in round two" }).ShouldBe("812-a-bleed-that-does-not-tick-after-a-stun.json");
    }

    [Fact]
    public async Task A_scenario_that_is_not_redacted_is_refused_and_nothing_is_written()
    {
        var zip = await ZipAsync(await File.ReadAllTextAsync(await GeneratedAsync(), TestContext.Current.CancellationToken));
        var output = Path.Combine(_root, "bugs", "812-x.json");

        (await ScenarioCommands.ImportAsync(Import(zip, output))).ShouldBe(1);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task A_redacted_scenario_that_does_not_rebuild_on_this_engine_is_refused()
    {
        var moved = JsonNode.Parse(await RedactedAsync())!.AsObject();
        moved["match"]!["seed"] = 6;
        var zip = await ZipAsync(moved.ToJsonString());
        var output = Path.Combine(_root, "bugs", "812-x.json");

        (await ScenarioCommands.ImportAsync(Import(zip, output))).ShouldBe(1);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task A_zip_that_holds_anything_but_one_scenario_json_is_refused()
    {
        var zip = Path.Combine(_root, "two.zip");
        await using (var archive = await ZipArchive.CreateAsync(File.Create(zip), ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: null, TestContext.Current.CancellationToken))
        {
            archive.CreateEntry("scenario.json");
            archive.CreateEntry("other.json");
        }

        (await ScenarioCommands.ImportAsync(Import(zip, Path.Combine(_root, "bugs", "812-x.json")))).ShouldBe(1);
        (await ScenarioCommands.ImportAsync(Import(Path.Combine(_root, "none.zip"), Path.Combine(_root, "bugs", "812-x.json")))).ShouldBe(1);
    }

    [Fact]
    public async Task A_traced_check_prints_each_seat_s_board_after_each_entry_past_the_cut()
    {
        var file = Path.Combine(_root, "redacted.json");
        await File.WriteAllTextAsync(file, await RedactedAsync(), TestContext.Current.CancellationToken);
        var said = new StringWriter();

        var held = await ScenarioCommands.CheckAsync(CliOptions.Parse(["scenario-check", "--trace", "--scenario", file, "--schema", Path.Combine(_root, "none.json")]), said);

        held.ShouldBe(0);
        var lines = said.ToString().Split('\n');
        lines.Count(line => line.StartsWith("After entry ", StringComparison.Ordinal)).ShouldBe(3);
        lines.Count(line => line.StartsWith("  player1, round 2", StringComparison.Ordinal)).ShouldBe(4);
        lines.ShouldContain(line => line.Contains(" health, ", StringComparison.Ordinal));
    }

    private CliOptions Import(string zip, string output) =>
        CliOptions.Parse(["scenario-import", "--zip", zip, "--issue", "812", "--out", output, "--schema", Path.Combine(_root, "none.json")]);

    /// <summary>A greedy 1v1 on seed 5 cut at round 2, as <c>scenario-generate</c> writes it.</summary>
    private async Task<string> GeneratedAsync()
    {
        var output = Path.Combine(_root, $"generated-{Guid.CreateVersion7():N}.json");
        var written = await ScenarioCommands.GenerateAsync(CliOptions.Parse(["scenario-generate", "--schema", Schema, "--seed", "5", "--format", "1v1", "--round", "2", "--out", output]));
        written.ShouldBe(0);
        return output;
    }

    /// <summary>The generated scenario as the panel's Download the scenario writes it: three answers past the cut, the report's ids.</summary>
    private async Task<string> RedactedAsync()
    {
        var document = ScenarioFile.Read(await File.ReadAllTextAsync(await GeneratedAsync(), TestContext.Current.CancellationToken));
        var scenario = RedactedScenario.Of(document.Scenario, Report, document.Scenario.Cut.CutAt + 3);
        return ScenarioFile.Write(document with { Scenario = scenario, Session = Report, Title = $"Report {Report}, round 2", Goal = null });
    }

    private async Task<string> ZipAsync(string scenario)
    {
        var zip = Path.Combine(_root, $"report-{Guid.CreateVersion7():N}.zip");
        await using var archive = await ZipArchive.CreateAsync(File.Create(zip), ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: null, TestContext.Current.CancellationToken);
        await using var entry = await archive.CreateEntry("scenario.json").OpenAsync(TestContext.Current.CancellationToken);
        await entry.WriteAsync(System.Text.Encoding.UTF8.GetBytes(scenario), TestContext.Current.CancellationToken);
        return zip;
    }
}

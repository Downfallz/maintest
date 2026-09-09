using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Cli;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Evaluation;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Tests.Studio;

/// <summary>
/// The studio's API over a scratch content directory: what it routes, what it writes, and what it refuses.
/// Nothing here starts a host or plays a match; the run endpoint is covered by <see cref="StudioRunnerTests"/>.
/// </summary>
public sealed class StudioApiTests : IDisposable
{
    private readonly StudioContent _content = new();
    private readonly StudioApi _api;

    public StudioApiTests()
    {
        var options = new CliOptions { Command = "studio", Output = "out.csv", SchemaPath = Path.Combine(_content.Path, "dst", "game.schema.json") };
        _api = new StudioApi(
            new ContentStore(_content.Path),
            new StudioRunner(options, Path.Combine(_content.Path, "runs"), TimeProvider.System),
            new BenchmarkStore(Path.Combine(_content.Path, "benchmarks")),
            RuleSet.Default,
            Path.Combine(_content.Path, "dst"));
    }

    public void Dispose()
    {
        _api.Dispose();
        _content.Dispose();
    }

    [Fact]
    public async Task The_catalogue_carries_every_authored_document_and_the_content_hash()
    {
        var result = await AcceptAsync("GET", "/api/catalogue", string.Empty);

        result.GetProperty("spells").GetArrayLength().ShouldBe(2);
        result.GetProperty("contentHash").GetString().ShouldNotBeNullOrWhiteSpace();
        result.GetProperty("problems").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task An_unknown_endpoint_is_a_404_naming_what_was_asked()
    {
        var response = await _api.HandleAsync("POST", "/api/nope", "{}");

        response.Status.ShouldBe(404);
        Payload(response).GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("/api/nope");
    }

    [Fact]
    public async Task Saving_a_document_writes_it_and_answers_with_the_catalogue_that_follows()
    {
        var body = """
            {
              "kind": "Spell",
              "path": "Spells/brawler/guard.v2.json",
              "document": {
                "id": "spell:guard:v2", "name": "Guard", "spellType": "Defensive", "creatureClass": "Brawler",
                "initiative": 2, "energyCost": 2, "criticalChance": 0,
                "targeting": { "origin": "Self", "scope": "SingleTarget" },
                "effects": [ { "kind": "DefenseBuff", "amount": 2, "permanent": true, "stacking": "Ignore" } ]
              }
            }
            """;

        var result = await AcceptAsync("POST", "/api/documents", body);

        result.GetProperty("saved").GetString().ShouldBe("Spells/brawler/guard.v2.json");
        File.Exists(Path.Combine(_content.Path, "Spells", "brawler", "guard.v2.json")).ShouldBeTrue();
        result.GetProperty("catalogue").GetProperty("spells").GetArrayLength().ShouldBe(3);
    }

    /// <summary>
    /// <c>Creature</c> is the zero of <see cref="ContentKind"/>, so a missing kind would otherwise mean "creature"
    /// and fail later with a message about the wrong folder.
    /// </summary>
    [Fact]
    public async Task A_request_without_a_kind_says_which_kinds_there_are()
    {
        var response = await _api.HandleAsync("POST", "/api/documents", """{"path":"Spells/x.v1.json","document":{"id":"spell:x:v1"}}""");

        response.Status.ShouldBe(400);
        Payload(response).GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("'kind' is required");
    }

    /// <summary>The page sets `create` when it cuts a version or makes an item, so the host guards the file.</summary>
    [Fact]
    public async Task A_document_the_page_calls_new_does_not_replace_one_that_is_there()
    {
        var body = """
            {
              "kind": "Spell",
              "path": "Spells/brawler/guard.v1.json",
              "create": true,
              "document": {
                "id": "spell:guard:v1", "name": "Replaced", "spellType": "Defensive", "creatureClass": "Brawler",
                "initiative": 2, "energyCost": 1, "criticalChance": 0,
                "targeting": { "origin": "Self", "scope": "SingleTarget" },
                "effects": [ { "kind": "DefenseBuff", "amount": 2, "permanent": true, "stacking": "Ignore" } ]
              }
            }
            """;

        var response = await _api.HandleAsync("POST", "/api/documents", body);

        response.Status.ShouldBe(400);
        Payload(response).GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("already exists");
        File.ReadAllText(Path.Combine(_content.Path, "Spells", "brawler", "guard.v1.json")).ShouldContain("\"name\": \"Guard\"");
    }

    [Fact]
    public async Task A_document_written_outside_the_folder_of_its_kind_is_refused()
    {
        var response = await _api.HandleAsync("POST", "/api/documents", """{"kind":"Spell","path":"../escape.json","document":{"id":"spell:x:v1"}}""");

        response.Status.ShouldBe(400);
        File.Exists(Path.Combine(_content.Path, "..", "escape.json")).ShouldBeFalse();
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_refused_rather_than_crashing_the_host()
    {
        (await _api.HandleAsync("POST", "/api/documents", "{ not json")).Status.ShouldBe(400);
        (await _api.HandleAsync("POST", "/api/documents", string.Empty)).Status.ShouldBe(400);
    }

    [Fact]
    public async Task Building_writes_the_consolidated_schema_and_answers_with_its_hash()
    {
        var result = await AcceptAsync("POST", "/api/build", "{}");

        var hash = result.GetProperty("contentHash").GetString().ShouldNotBeNull();
        hash.Length.ShouldBe(64);
        File.Exists(Path.Combine(_content.Path, "dst", "game.schema.json")).ShouldBeTrue();
    }

    [Fact]
    public async Task Content_that_does_not_build_answers_with_every_problem_rather_than_a_hash()
    {
        await _api.HandleAsync("POST", "/api/aliases", """{"aliases":{"spell:strike":"spell:gone:v1"}}""");

        var response = await _api.HandleAsync("POST", "/api/build", "{}");

        response.Status.ShouldBe(400);
        Payload(response).GetProperty("problems").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task A_run_page_for_a_run_that_does_not_exist_is_a_404_and_never_leaves_the_runs_directory()
    {
        _api.RunPage("no-such-run", Path.Combine(AppContext.BaseDirectory, "viewer")).Status.ShouldBe(404);
        _api.RunPage("../../etc", Path.Combine(AppContext.BaseDirectory, "viewer")).Status.ShouldBe(404);
    }

    [Fact]
    public async Task The_audit_reports_what_no_creature_can_reach_and_what_a_match_cannot_tell_apart()
    {
        // A spell nothing teaches and nobody starts with: valid content the builder has no reason to refuse.
        _content.Write("Spells/lost.v1.json", StudioContent.Guard.Replace("guard:v1", "lost:v1").Replace("\"Guard\"", "\"Lost\""));

        var result = await AcceptAsync("GET", "/api/audit", string.Empty);

        var findings = result.GetProperty("audit").GetProperty("findings").EnumerateArray().ToList();
        findings.Select(finding => finding.GetProperty("code").GetString()).ShouldContain("Spell.Unreachable");
        findings.Select(finding => finding.GetProperty("subject").GetString()).ShouldContain("spell:lost:v1");
    }

    [Fact]
    public async Task The_audit_says_whether_this_content_has_a_benchmark_digest()
    {
        var result = await AcceptAsync("GET", "/api/audit", string.Empty);

        result.GetProperty("benchmark").GetProperty("exists").GetBoolean().ShouldBeFalse("scratch content has never been benchmarked");
        result.GetProperty("benchmark").GetProperty("path").GetString().ShouldNotBeNull()
            .ShouldContain(result.GetProperty("audit").GetProperty("contentVersion").GetString()!);
    }

    [Fact]
    public async Task The_audit_counts_what_it_looked_at_and_says_which_rules_it_assumed()
    {
        var result = (await AcceptAsync("GET", "/api/audit", string.Empty)).GetProperty("audit");

        result.GetProperty("creatures").GetInt32().ShouldBe(1);
        result.GetProperty("spells").GetInt32().ShouldBe(2);
        result.GetProperty("roundCap").GetInt32().ShouldBe(RuleSet.Default.RoundCap);
        result.GetProperty("reach").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Content_that_does_not_build_cannot_be_audited_and_says_so()
    {
        await _api.HandleAsync("POST", "/api/aliases", """{"aliases":{"spell:strike":"spell:gone:v1"}}""");

        (await _api.HandleAsync("GET", "/api/audit", string.Empty)).Status.ShouldBe(400);
    }

    /// <summary>The page builds one box per weight from this, so it grows a field when the engine grows a weight.</summary>
    [Fact]
    public async Task The_weights_endpoint_hands_the_page_the_engine_s_own_defaults()
    {
        var result = await AcceptAsync("GET", "/api/weights", string.Empty);

        result.GetProperty("order").EnumerateArray().Select(name => name.GetString()).ShouldBe(
            ["damage", "kill", "heal", "stun", "bleed", "buff", "energy", "risk"]);
        result.GetProperty("values").GetProperty("kill").GetDouble().ShouldBe(ScoringWeights.Default.Kill);
        result.GetProperty("fingerprint").GetString().ShouldBe(ScoringWeights.Default.Fingerprint);
    }

    [Fact]
    public async Task The_run_list_of_a_studio_that_has_played_nothing_is_empty()
    {
        (await AcceptAsync("GET", "/api/runs", string.Empty)).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void A_comparison_of_a_run_with_itself_is_refused_rather_than_drawn_with_no_delta()
    {
        var response = _api.ComparePage("a-run", "a-run", Path.Combine(AppContext.BaseDirectory, "viewer"));

        response.Status.ShouldBe(400);
    }

    [Fact]
    public void A_comparison_naming_a_run_that_does_not_exist_is_a_404()
    {
        _api.ComparePage("one", "two", Path.Combine(AppContext.BaseDirectory, "viewer")).Status.ShouldBe(404);
    }

    private async Task<JsonElement> AcceptAsync(string method, string path, string body)
    {
        var response = await _api.HandleAsync(method, path, body);
        var payload = Payload(response);
        response.Status.ShouldBe(200, payload.ToString());
        return payload.GetProperty("result");
    }

    private static JsonElement Payload(StudioResponse response) =>
        JsonDocument.Parse(response.Body).RootElement.Clone();
}

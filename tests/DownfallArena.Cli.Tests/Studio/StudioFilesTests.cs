using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.Infrastructure.Resources.Schema;

namespace DownfallArena.Cli.Tests.Studio;

/// <summary>
/// The static routes are a fixed table, not a path lookup, so no request can name a file the studio does not
/// ship. These run against the real <c>studio/</c> and <c>viewer/</c> files copied next to the tests, which is
/// what makes them catch a module the page imports and the table does not serve: the page is a blank screen
/// then, and nothing else would say so.
/// </summary>
public sealed class StudioFilesTests
{
    private static StudioFiles Files => new(
        Path.Combine(AppContext.BaseDirectory, "studio"),
        Path.Combine(AppContext.BaseDirectory, "viewer"));

    [Theory]
    [InlineData("/", "Downfall Arena content studio")]
    [InlineData("/index.html", "<script type=\"module\" src=\"studio.js\">")]
    [InlineData("/studio.js", "import { backendForThisPage } from './backend.js';")]
    [InlineData("/backend.js", "/api/catalogue")]
    [InlineData("/backend.js", "export function hostedBackend(")]
    [InlineData("/github.js", "export function githubBackend(")]
    [InlineData("/studio.css", ".banner")]
    [InlineData("/viewer.css", "--ink")]
    public void Every_file_the_page_asks_for_is_served(string path, string expected)
    {
        var response = Files.Get(path);

        response.Status.ShouldBe(200);
        System.Text.Encoding.UTF8.GetString(response.Body).ShouldContain(expected);
    }

    /// <summary>
    /// Every module beside the page is routed, and every module the page publishes is copied by the workflow. A
    /// list with a row per file cannot notice a new one, and a module that is not served is a page that dies at
    /// its first import -- silently, because nothing else asks for it.
    /// </summary>
    [Fact]
    public void Every_module_in_the_studio_directory_is_both_served_and_published()
    {
        var modules = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "studio"), "*.js")
            .Select(Path.GetFileName)
            .Where(name => !name!.EndsWith(".test.js", StringComparison.Ordinal))
            .ToList();
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "pages.yml"));

        modules.ShouldNotBeEmpty();
        foreach (var module in modules)
        {
            Files.Get($"/{module}").Status.ShouldBe(200, $"{module} sits beside the page but the host does not serve it");
            workflow.ShouldContain($"studio/{module}", Case.Sensitive, $"{module} is served but pages.yml never copies it");
        }
    }

    /// <summary>
    /// The page writes an effect's stacking policy explicitly, from its own <c>EFFECTS</c> table, so that table
    /// holds a copy of a default that lives in <see cref="GameSchemaMapper"/>. A copy drifts: ADR 0041 moved the
    /// per-round family to <c>Stack</c> and the page went on seeding <c>Refresh</c>, which would have made every
    /// studio-authored bleed pin the old behaviour while the 36 authored by hand followed the new rule.
    ///
    /// So this asserts the two against each other rather than against a literal: for each kind the page seeds, it
    /// maps that effect through the engine with no <c>stacking</c> field and compares what the engine chose with
    /// what the page would have written. It is a text read of <c>studio.js</c> because the table is in the page
    /// script, which has no export seam (ADR 0024) -- but the value it is held to is the engine's own, computed by
    /// running it.
    /// </summary>
    [Fact]
    public void The_stacking_the_page_seeds_is_the_one_the_engine_falls_back_to()
    {
        var page = System.Text.Encoding.UTF8.GetString(Files.Get("/studio.js").Body);
        var seeded = System.Text.RegularExpressions.Regex
            .Matches(page, @"^\s*(?<kind>\w+): \{[^}]*stacking: '(?<policy>\w+)'", System.Text.RegularExpressions.RegexOptions.Multiline)
            .ToDictionary(match => match.Groups["kind"].Value, match => match.Groups["policy"].Value, StringComparer.Ordinal);

        seeded.Count.ShouldBe(8, "the page seeds a policy for every lasting kind; a new one needs a row here too");
        foreach (var (kind, policy) in seeded)
        {
            FallbackFor(kind).ToString().ShouldBe(policy, $"the page seeds {kind} at {policy} and the engine falls back to something else");
        }
    }

    /// <summary>What the mapper makes of one effect of this kind that says nothing about stacking.</summary>
    private static StackingPolicy FallbackFor(string kind)
    {
        var effect = new EffectDto { Kind = kind, Amount = 1, AmountPerRound = 1, DurationRounds = 1 };
        var schema = new GameSchema
        {
            Spells =
            [
                new SpellDto
                {
                    Id = "spell:probe:v1",
                    Name = "Probe",
                    SpellType = "Offensive",
                    CreatureClass = "Creature",
                    Targeting = new TargetingDto { Origin = "Enemy", Scope = "SingleTarget" },
                    Effects = [effect],
                },
            ],
        };

        var spell = GameSchemaMapper.ToGameResources(schema).Spells.ShouldHaveSingleItem();
        return spell.Effects.ShouldHaveSingleItem().ShouldBeAssignableTo<LastingEffect>()!.Stacking;
    }

    /// <summary>The browser asks for an icon the studio does not ship; a 404 would be a console error on every load.</summary>
    [Fact]
    public void The_icon_the_browser_asks_for_on_its_own_is_answered_empty()
    {
        var response = Files.Get("/favicon.ico");

        response.Status.ShouldBe(204);
        response.Body.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/../Directory.Build.props")]
    [InlineData("/studio/studio.js")]
    [InlineData("/appsettings.json")]
    [InlineData("/api/catalogue")]
    public void Anything_else_is_a_404(string path)
    {
        Files.Get(path).Status.ShouldBe(404);
    }
}

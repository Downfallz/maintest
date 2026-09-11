using System.Globalization;
using System.Text.RegularExpressions;

namespace DownfallArena.Cli.Tests.Studio;

/// <summary>
/// The page and its script are two files that have to agree on the ids between them, and nothing in either one
/// says so. A `$('save')` against markup that calls it `save-button` is `null` at load, which in a page built
/// from one module is not one broken button but every line after it -- and no test of the served bytes, nor any
/// test of the modules in Node, can see it. This is the closest thing to a DOM test the repository has.
/// </summary>
public sealed class StudioPageContractTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(5);

    private static readonly string Page = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "studio", "index.html"));
    private static readonly string Script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "studio", "studio.js"));

    [Fact]
    public void Every_element_the_script_asks_for_is_one_the_page_has_or_one_it_builds()
    {
        var asked = Ids(Script, @"\$\('([a-z0-9-]+)'\)");
        var inMarkup = Ids(Page, @"id=""([a-z0-9-]+)""");
        // Some are created at runtime rather than written in the markup, which is just as good a source.
        var built = Ids(Script, @"id: '([a-z0-9-]+)'");

        asked.ShouldNotBeEmpty();
        asked.Except(inMarkup).Except(built).ShouldBeEmpty("the script reaches for elements that never exist");
    }

    /// <summary>
    /// A panel is three things that must line up: the section, the toolbar button that opens it, and its name in
    /// the script's list. Two out of three is a button that opens nothing, or a panel that no longer closes when
    /// another opens.
    /// </summary>
    [Fact]
    public void Every_panel_has_its_section_its_button_and_its_place_in_the_list()
    {
        var listed = Regex.Match(Script, @"const PANELS = \[([^\]]+)\]", RegexOptions.None, MatchTimeout).Groups[1].Value;
        var panels = Ids(listed, @"'([a-z]+)'");

        panels.ShouldNotBeEmpty();
        foreach (var panel in panels)
        {
            Page.ShouldContain($@"id=""{panel}"" class=""panel""", Case.Sensitive, $"{panel} is in PANELS with no section to open");
            Page.ShouldContain($@"id=""{panel}-panel""", Case.Sensitive, $"{panel} is in PANELS with no button to open it");
            Page.ShouldContain($@"data-close=""{panel}""", Case.Sensitive, $"{panel} has no way to close it");
        }
    }

    /// <summary>
    /// On a phone the toolbar is a fixed-height bar along the bottom, so a button past its column count lands on
    /// a second row that the bar's height crops away: present in the markup, reachable by a screen reader, and
    /// invisible to the person holding the phone. The published page has one button the local page does not,
    /// which is exactly how a count written once goes stale -- and it looks fine on a desk, where the same bar
    /// is an inline flex row.
    /// </summary>
    [Fact]
    public void The_phone_toolbar_has_room_for_every_button_it_holds()
    {
        var css = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "studio", "studio.css"));
        var buttons = Regex.Count(Page, "class=\"tool\"", RegexOptions.None, MatchTimeout);
        var bar = Regex.Match(css, @"\.toolbar \{(.+?)\}", RegexOptions.Singleline, MatchTimeout).Groups[1].Value;
        var pinned = Regex.Match(bar, @"grid-template-columns:\s*repeat\((\d+)", RegexOptions.None, MatchTimeout);

        buttons.ShouldBeGreaterThan(0);
        bar.ShouldNotBeEmpty();
        if (pinned.Success)
        {
            int.Parse(pinned.Groups[1].Value, CultureInfo.InvariantCulture)
                .ShouldBeGreaterThanOrEqualTo(buttons, "the bottom bar pins fewer columns than it has buttons, so the last ones fall off a phone");
        }
    }

    /// <summary>
    /// A class the script puts on an element and the stylesheet never mentions does not crash anything, which is
    /// exactly why it survives: the element is simply unstyled, and an unstyled control on a phone is a worse
    /// failure than a missing one, because it looks like a decision. A class earns its keep without a rule only
    /// when the script queries it as a hook, so that is a named exception rather than a blanket excuse.
    /// </summary>
    [Fact]
    public void Every_class_the_script_sets_is_styled_or_queried_as_a_hook()
    {
        var css = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "studio", "studio.css"));
        var set = Regex.Matches(Script, "className: '([a-z0-9 -]+)'", RegexOptions.None, MatchTimeout)
            .SelectMany(match => match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.Ordinal);
        var hooks = Ids(Script, @"querySelectorAll?\('\.([a-z0-9-]+)");

        set.ShouldNotBeEmpty();
        set.Where(name => !Regex.IsMatch(css, $@"\.{Regex.Escape(name)}(?![\w-])", RegexOptions.None, MatchTimeout))
            .Except(hooks)
            .ShouldBeEmpty("the script styles elements with classes the stylesheet does not have");
    }

    /// <summary>
    /// GitHub Pages sends no cache headers we control, so a browser will happily keep yesterday's `studio.js`
    /// beside today's `index.html` -- and new markup driven by old script is the worst kind of broken, because
    /// it looks right and does nothing. The publish step stamps every reference with the commit that published
    /// it; a module imported without a stamp is one that can be served stale on its own.
    /// </summary>
    [Fact]
    public void Every_reference_between_the_published_files_is_stamped_with_its_commit()
    {
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "pages.yml"));
        var modules = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "studio"), "*.js")
            .Where(path => !path.EndsWith(".test.js", StringComparison.Ordinal));
        var imported = modules
            .SelectMany(path => Ids(File.ReadAllText(path), @"from '\./([a-z0-9.-]+)'"))
            .ToHashSet(StringComparer.Ordinal);
        var linked = Ids(Page, @"(?:src|href)=""([a-z0-9.-]+\.(?:js|css))""");

        imported.ShouldNotBeEmpty();
        linked.ShouldNotBeEmpty();
        foreach (var name in imported.Concat(linked))
        {
            workflow.ShouldContain($"{name}?v=$GITHUB_SHA", Case.Sensitive, $"{name} is referenced by the page but the publish step never stamps it, so a browser can serve it stale beside fresher files");
        }
    }

    private static HashSet<string> Ids(string text, string pattern) =>
        Regex.Matches(text, pattern, RegexOptions.None, MatchTimeout)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
}

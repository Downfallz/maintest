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

    private static HashSet<string> Ids(string text, string pattern) =>
        Regex.Matches(text, pattern, RegexOptions.None, MatchTimeout)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
}

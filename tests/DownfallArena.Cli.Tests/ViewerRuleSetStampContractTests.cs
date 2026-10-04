using System.Text.Json;
using System.Text.RegularExpressions;

namespace DownfallArena.Cli.Tests;

/// <summary>
/// A run stamp outlives the names it was written with: the evolution allowance has been spelled two ways, and
/// this repository's own <c>viewer/samples</c> still carry the older one. A page that reads only today's name
/// renders a dash for every artifact recorded before the rename -- and reading only a name no stamp has at all
/// is how that line came to print <c>undefined</c>. This runs against the real <c>viewer/index.html</c>, the
/// only check the page has.
/// </summary>
public sealed class ViewerRuleSetStampContractTests
{
    private static readonly string Viewer = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "viewer", "index.html"));

    private static readonly string Samples = Path.Combine(AppContext.BaseDirectory, "viewer", "samples");

    /// <summary>
    /// Every spelling of the evolution allowance the shipped artifacts carry is one the page reads. The
    /// samples are the viewer's own fixtures: a shape it cannot render is a page that broke on its own demo.
    /// </summary>
    [Fact]
    public void The_viewer_reads_every_allowance_spelling_its_own_samples_carry()
    {
        var spellings = AllowanceSpellings().ToList();

        spellings.ShouldNotBeEmpty($"no stamp was found under '{Samples}', so this asserts nothing");
        spellings.ShouldContain("evolutionPicksPerRound", "the samples are older than the rename, which is what makes them worth holding the page against");

        // The function's body, not the whole page: a spelling named only in a comment is a spelling the page
        // does not read, and an assertion over the file would pass on the comment that explains the fallback.
        var read = Read("picksAnOpportunity");
        foreach (var spelling in spellings)
        {
            read.ShouldContain(spelling, $"a sample stamp spells the allowance '{spelling}', and the viewer renders that sample");
        }
    }

    /// <summary>The rule set properties a function of the page reads, with its comments stripped.</summary>
    private static HashSet<string> Read(string function)
    {
        var body = Regex.Replace(BodyOf(function), LineComment, string.Empty, RegexOptions.None, MatchTimeout);
        return [.. Regex.Matches(body, RuleSetRead, RegexOptions.None, MatchTimeout).Select(match => match.Groups[1].Value)];
    }

    /// <summary>A line comment: a spelling named in one is a spelling the page does not read.</summary>
    private const string LineComment = @"//[^\r\n]*";

    private const string RuleSetRead = @"\brules\.(\w+)\b";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(5);

    /// <summary>One function of the page, by brace depth, as the viewer's other contract tests read it.</summary>
    private static string BodyOf(string function)
    {
        var open = Viewer.IndexOf('{', Viewer.IndexOf($"function {function}(", StringComparison.Ordinal));
        var depth = 0;
        for (var index = open; index < Viewer.Length; index++)
        {
            depth += Viewer[index] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return Viewer[(open + 1)..index];
            }
        }

        throw new InvalidDataException($"The body of {function} is not closed in the viewer.");
    }

    /// <summary>Every way the allowance is spelled in a rule set of a stamp under <c>viewer/samples</c>.</summary>
    private static IEnumerable<string> AllowanceSpellings() =>
        Directory.EnumerateFiles(Samples, "*.json").Concat(Directory.EnumerateFiles(Samples, "*.jsonl"))
            .SelectMany(path => File.ReadLines(path).Where(line => line.Contains("ruleSet", StringComparison.Ordinal)).DefaultIfEmpty(File.ReadAllText(path)))
            .SelectMany(RuleSetNames)
            .Where(name => name.StartsWith("evolutionPicks", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal);

    /// <summary>The rule set's property names in one artifact, wherever its stamp sits in the document.</summary>
    private static IEnumerable<string> RuleSetNames(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            return [.. RuleSets(document.RootElement).SelectMany(rules => rules.EnumerateObject().Select(property => property.Name))];
        }
    }

    private static IEnumerable<JsonElement> RuleSets(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("ruleSet") && property.Value.ValueKind == JsonValueKind.Object)
                    {
                        yield return property.Value;
                    }

                    foreach (var nested in RuleSets(property.Value))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in RuleSets(item))
                    {
                        yield return nested;
                    }
                }

                break;
            default:
                break;
        }
    }
}

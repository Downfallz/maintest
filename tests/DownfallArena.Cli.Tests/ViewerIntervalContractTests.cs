using System.Text.Json;
using System.Text.RegularExpressions;
using DownfallArena.Application.Evaluation;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Tests;

/// <summary>
/// A win rate reaches the viewer two ways: an evaluation carries a <see cref="ConfidenceInterval"/> the engine
/// serialized, and a batch is summarized by the viewer's own <c>wilson</c>. The same <c>rateStat</c> renders
/// both, so the two shapes must be one shape. They were not: <c>wilson</c> built <c>rate</c> where the engine
/// writes <c>mean</c>, and every evaluation's win rate rendered as a dash above a correct interval. These run
/// against the real <c>viewer/index.html</c>, the only check the page has.
/// </summary>
public sealed class ViewerIntervalContractTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(5);

    private static readonly string Viewer = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "viewer", "index.html"));

    /// <summary>The property names the engine writes for an interval, from the engine's own serializer.</summary>
    private static List<string> EngineNames =>
        [.. JsonSerializer.SerializeToElement(new ConfidenceInterval(0.5, 0.4, 0.6), ArtifactJson.DocumentOptions)
            .EnumerateObject().Select(property => property.Name)];

    [Fact]
    public void The_viewer_reads_an_interval_by_the_names_the_engine_writes()
    {
        var interval = ParameterOf("rateStat", 1);
        var read = Regex.Matches(BodyOf("rateStat"), $@"\b{interval}\.(\w+)\b", RegexOptions.None, MatchTimeout)
            .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        read.ShouldNotBeEmpty();
        read.ShouldBeSubsetOf(EngineNames);
    }

    [Fact]
    public void The_intervals_the_viewer_computes_itself_have_the_shape_the_engine_writes()
    {
        var returns = Regex.Matches(BodyOf("wilson"), @"return\s*\{([^}]*)\}", RegexOptions.None, MatchTimeout);

        returns.ShouldNotBeEmpty();
        foreach (Match returned in returns)
        {
            var names = Regex.Matches(returned.Groups[1].Value, @"(\w+)\s*:", RegexOptions.None, MatchTimeout)
                .Select(match => match.Groups[1].Value).ToList();
            names.ShouldBe(EngineNames, ignoreOrder: true);
        }
    }

    /// <summary>The name of a function's parameter at a position, from its declaration in the page.</summary>
    private static string ParameterOf(string function, int position) =>
        Signature(function).Split(',')[position].Trim();

    /// <summary>The text between the parentheses of a function's declaration.</summary>
    private static string Signature(string function)
    {
        var start = Viewer.IndexOf($"function {function}(", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"the viewer no longer declares {function}");
        var open = Viewer.IndexOf('(', start);
        return Viewer[(open + 1)..Viewer.IndexOf(')', open)];
    }

    /// <summary>The body of a function declared in the page, braces balanced.</summary>
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
}

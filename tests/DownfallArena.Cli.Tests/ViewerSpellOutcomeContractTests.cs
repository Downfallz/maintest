using System.Text.Json;
using System.Text.RegularExpressions;
using DownfallArena.Application.Evaluation;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Tests;

/// <summary>
/// The comparison the studio opens on two runs totals each evaluation's spell outcomes by spell name, which
/// means reading fields off a <see cref="SpellOutcome"/> the engine serialized. A rename on either side would
/// leave the table silently full of zeroes rather than fail anything, so this holds the two together against the
/// real <c>viewer/index.html</c>.
/// </summary>
public sealed class ViewerSpellOutcomeContractTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(5);

    private static readonly string Viewer = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "viewer", "index.html"));

    [Fact]
    public void The_comparison_reads_a_spell_outcome_by_the_names_the_engine_writes()
    {
        var outcome = new SpellOutcome { Spell = "spell:strike:v1", Intents = 4, Sides = 2, Wins = 1, Losses = 1, Draws = 0 };
        var written = JsonSerializer.SerializeToElement(outcome, ArtifactJson.DocumentOptions)
            .EnumerateObject().Select(property => property.Name).ToList();

        var read = Regex.Matches(BodyOf("spellsByName"), @"\boutcome\.(\w+)\b", RegexOptions.None, MatchTimeout)
            .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        read.ShouldNotBeEmpty();
        read.ShouldBeSubsetOf(written);
    }

    /// <summary>The list of outcomes itself, which the comparison reaches for on the evaluation.</summary>
    [Fact]
    public void The_comparison_reads_the_outcome_list_by_the_name_the_engine_writes()
    {
        var written = ArtifactJson.DocumentOptions.PropertyNamingPolicy
            .ShouldNotBeNull()
            .ConvertName(nameof(EvaluationResult.SpellOutcomes));

        BodyOf("spellDeltaTable").ShouldContain(written);
    }

    private static string BodyOf(string function)
    {
        var start = Viewer.IndexOf($"function {function}(", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"the viewer no longer declares {function}");
        var open = Viewer.IndexOf('{', start);
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

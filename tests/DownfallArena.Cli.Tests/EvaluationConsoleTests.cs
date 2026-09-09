using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Learning;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Tests;

/// <summary>
/// What the console makes of an evaluation. The spell table is the part worth pinning: it carries numbers that
/// are misread when shown bare, and the lines that make them readable are as much the output as the table is.
/// </summary>
public sealed class EvaluationConsoleTests
{
    [Fact]
    public void The_spell_table_shows_what_a_spell_did_beside_how_often_it_was_chosen()
    {
        var printed = Print(Outcome("spell:strike:v1", sides: 10, wins: 7, resolved: 40, fizzled: 10, damage: 120, resolvedWhenWon: 30));

        printed.ShouldContain("Resolve");
        printed.ShouldContain("Damage");
        // 40 of 50 declarations landed, and the 120 damage they dealt.
        printed.ShouldContain("80.0 %");
        printed.ShouldContain("120");
    }

    /// <summary>
    /// A winning side survives longer and casts more of everything, so every spell's winner share is pulled up
    /// together. Printed alone the column reads as a result; the baseline is what makes it one.
    /// </summary>
    [Fact]
    public void Won_cast_is_printed_with_the_baseline_it_has_to_be_read_against()
    {
        var printed = Print(
            Outcome("spell:strike:v1", sides: 10, wins: 5, resolved: 60, fizzled: 0, damage: 60, resolvedWhenWon: 36),
            Outcome("spell:guard:v1", sides: 10, wins: 5, resolved: 40, fizzled: 0, damage: 0, resolvedWhenWon: 24));

        // 60 of the 100 landed casts were made by a side that won, so that is where the column's neutral is.
        printed.ShouldContain("Won-cast reads against 60.0 %, not one half");
    }

    [Fact]
    public void A_run_too_short_to_rank_any_spell_says_so_rather_than_printing_nothing()
    {
        var printed = Print(Outcome("spell:strike:v1", sides: 2, wins: 1, resolved: 4, fizzled: 0, damage: 4, resolvedWhenWon: 2));

        printed.ShouldContain("No spell was declared by 8 sides or more");
        printed.ShouldNotContain("Won-cast reads against");
    }

    [Fact]
    public void An_evaluation_that_recorded_no_cast_prints_the_table_without_a_baseline_it_cannot_compute()
    {
        var printed = Print(Outcome("spell:strike:v1", sides: 10, wins: 5, resolved: 0, fizzled: 0, damage: 0, resolvedWhenWon: 0));

        printed.ShouldContain("spell:strike:v1");
        printed.ShouldNotContain("Won-cast reads against");
    }

    private static string Print(params SpellOutcome[] outcomes)
    {
        var writer = new StringWriter();
        EvaluationConsole.Print(Evaluation(outcomes), writer);
        return writer.ToString();
    }

    private static SpellOutcome Outcome(string spell, int sides, int wins, int resolved, int fizzled, int damage, int resolvedWhenWon) => new()
    {
        Spell = spell,
        Intents = resolved + fizzled,
        Sides = sides,
        Wins = wins,
        Losses = sides - wins,
        Draws = 0,
        Resolved = resolved,
        Fizzled = fizzled,
        Damage = damage,
        ResolvedWhenWon = resolvedWhenWon,
    };

    private static EvaluationResult Evaluation(IReadOnlyList<SpellOutcome> outcomes) => new()
    {
        Stamp = new RunStamp
        {
            EngineVersion = "abc123",
            ContentHash = "content",
            RuleSet = RuleSetStamp.Of(RuleSet.Default),
            FeatureSchema = "features:v1",
            Player1Agent = "Random",
            Player2Agent = "Greedy",
            BaseSeed = 1,
        },
        AgentA = Report("Random"),
        AgentB = Report("Greedy"),
        Matches = 10,
        Draws = 0,
        AverageRounds = 12.5,
        RoundCapShare = 0,
        SpellOutcomes = outcomes,
        Pairs = [],
    };

    private static AgentReport Report(string agent) => new()
    {
        Agent = agent,
        Wins = 5,
        WinRate = new ConfidenceInterval(0.5, 0.4, 0.6),
        Score = new ConfidenceInterval(0.5, 0.4, 0.6),
        AverageRemainingHealth = 10,
        SpellUsage = new Dictionary<string, int>(StringComparer.Ordinal) { ["spell:strike:v1"] = 4 },
        SpellEntropy = 0.5,
        Actions = 50,
        Fizzles = 5,
        Criticals = 2,
    };
}

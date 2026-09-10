using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Learning;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources.Effects;

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

    /// <summary>
    /// Every lasting effect the recorder counts has a column of its own, or a spell that only applies that
    /// effect reads as doing nothing in the one table balance is judged from. The count is taken from the
    /// domain, so a kind added to the taxonomy fails here rather than going quietly missing.
    /// </summary>
    [Fact]
    public void The_spell_table_has_a_column_for_every_lasting_effect_the_recorder_counts()
    {
        string[] columns = ["Stun", "Bleed", "Regen", "EnRegen", "Def", "Init"];
        var kinds = typeof(LastingEffect).Assembly.GetTypes()
            .Count(type => type.IsSubclassOf(typeof(LastingEffect)) && !type.IsAbstract);

        var printed = Print(Outcome("spell:healing_screech:v1", sides: 10, wins: 5, resolved: 8, fizzled: 0, damage: 0, resolvedWhenWon: 4)
            with
        { Healing = 16, Regens = 8 });

        columns.Length.ShouldBe(kinds, "one column per lasting effect kind in the domain");
        foreach (var column in columns)
        {
            printed.ShouldContain(column);
        }

        // The eight regenerations it applied, next to the 16 healing its instant half gave.
        printed.ShouldContain("16");
        printed.ShouldContain("8");
    }

    /// <summary>
    /// Energy is the one thing a spell can hand out that moves no health, so without its own column a spell
    /// that only restores energy reads as a blank row.
    /// </summary>
    [Fact]
    public void The_spell_table_shows_the_energy_a_spell_handed_back()
    {
        var printed = Print(Outcome("spell:summon_minions:v1", sides: 10, wins: 5, resolved: 9, fizzled: 0, damage: 0, resolvedWhenWon: 5)
            with
        { Energy = 27, EnergyRegenerations = 3 });

        printed.ShouldContain("Energy");
        printed.ShouldContain("27");
    }

    /// <summary>
    /// Defense and initiative are different stats: one column for both cannot say which one a spell moved.
    /// </summary>
    [Fact]
    public void Defense_and_initiative_conditions_are_counted_apart()
    {
        var printed = Print(Outcome("spell:ice_spear:v1", sides: 10, wins: 6, resolved: 20, fizzled: 0, damage: 80, resolvedWhenWon: 12)
            with
        { DefenseBuffs = 0, InitiativeDebuffs = 17 });

        var rows = printed.Split('\n').Where(line => line.Contains("spell:ice_spear:v1", StringComparison.Ordinal)).ToList();

        rows.Count.ShouldBe(1);
        rows[0].ShouldEndWith("17", Case.Sensitive, "the initiative debuffs land in the last column, not folded into the defense one");
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

using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The rule set a table plays is an input, never a silent default: the board game is balanced for 8 to 16
/// rounds and the engine's default caps at thirty, so a table that quietly took one would be a playtest of a
/// different game (docs/tabletop/playtest-app.md, decision 2).
/// </summary>
public sealed class RuleSetFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("table-rules").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void A_rule_set_file_is_read_as_the_five_numbers_it_names()
    {
        var path = Written("""{"teamSize":4,"energyPerRound":3,"evolutionPicksPerRound":1,"roundCap":12,"criticalMultiplier":1.5}""");

        var rules = RuleSetFile.Read(path);

        rules.TeamSize.ShouldBe(4);
        rules.EnergyPerRound.ShouldBe(3);
        rules.EvolutionPicksPerRound.ShouldBe(1);
        rules.RoundCap.ShouldBe(12);
        rules.CriticalMultiplier.ShouldBe(1.5);
    }

    /// <summary>The tabletop numbers, so a file only has to say what it changes.</summary>
    [Fact]
    public void A_number_the_file_leaves_out_is_the_tabletop_one_rather_than_the_simulator_s()
    {
        var rules = RuleSetFile.Read(Written("""{"teamSize":4}"""));

        rules.TeamSize.ShouldBe(4);
        rules.RoundCap.ShouldBe(12);
        rules.RoundCap.ShouldBeLessThan(RuleSet.Default.RoundCap);
    }

    /// <summary>A rule set the engine refuses is one line at start-up, not a stack five decisions into a match.</summary>
    [Fact]
    public void A_rule_set_the_engine_would_refuse_is_refused_while_reading()
    {
        var path = Written("""{"teamSize":0}""");

        Should.Throw<ArgumentException>(() => RuleSetFile.Read(path))
            .Message.ShouldContain("not playable");
    }

    [Fact]
    public void A_file_that_is_not_json_names_itself_and_says_so()
    {
        var path = Written("teamSize: 4");

        Should.Throw<ArgumentException>(() => RuleSetFile.Read(path))
            .Message.ShouldContain("not valid JSON");
    }

    [Fact]
    public void A_rule_set_that_is_not_there_says_what_one_looks_like()
    {
        Should.Throw<ArgumentException>(() => RuleSetFile.Read(Path.Combine(_directory, "absent.json")))
            .Message.ShouldContain("teamSize");
    }

    /// <summary>
    /// The line the table prints and a print sheet carries, in the same order: checking that a deck and an app
    /// are the same game is one glance by a human until components.md's question 6 is answered.
    /// </summary>
    [Fact]
    public void The_line_a_table_prints_names_the_five_numbers_and_where_they_came_from()
    {
        var described = RuleSetFile.Describe(RuleSet.Create(4, 3, 1, 12, 1.5), "tabletop.json");

        described.ShouldBe("Rules 4 creatures, 3 energy, 1 picks, 12 rounds, x1.5 crit (tabletop.json)");
    }

    /// <summary>Playing the default is allowed; not saying so is not.</summary>
    [Fact]
    public void A_table_given_no_rule_set_says_it_is_playing_the_engine_default()
    {
        var described = RuleSetFile.Describe(RuleSet.Default, path: null);

        described.ShouldContain($"{RuleSet.Default.RoundCap} rounds");
        described.ShouldContain("no --rules given");
    }

    private string Written(string json)
    {
        var path = Path.Combine(_directory, "rules.json");
        File.WriteAllText(path, json);
        return path;
    }
}

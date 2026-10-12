using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Tests;

/// <summary>The rule set a command line plays, from <c>--format</c> and <c>--declaration</c>.</summary>
public sealed class GameSessionRulesTests
{
    [Fact]
    public void A_command_line_naming_neither_plays_the_default_rule_set() =>
        GameSession.RulesOf(CliOptions.Parse(["play"])).ShouldBe(RuleSet.Default);

    /// <summary>
    /// Parsing the option is not playing it: the rule set the matches are created on is what has to change, or
    /// a run asked for at activation would play hidden intents and stamp itself so (ADR 0110).
    /// </summary>
    [Fact]
    public void A_declaration_reaches_the_rule_set_the_matches_play()
    {
        var rules = GameSession.RulesOf(CliOptions.Parse(["simulate", "--declaration", "at-activation"]));

        rules.ShouldBe(RuleSet.Default.WithCombatDeclaration(CombatDeclaration.AtActivation));
    }

    [Fact]
    public void A_format_and_a_declaration_apply_together()
    {
        var rules = GameSession.RulesOf(CliOptions.Parse(["evaluate", "--format", "1v1", "--declaration", "at-activation"]));

        rules.TeamSize.ShouldBe(1);
        rules.CombatDeclaration.ShouldBe(CombatDeclaration.AtActivation);
    }
}

using DownfallArena.Domain.Matches;

namespace DownfallArena.Domain.Tests.Matches;

public sealed class RuleSetTests
{
    [Fact]
    public void The_default_rule_set_matches_the_prototype()
    {
        RuleSet.Default.ShouldBe(RuleSet.Create(
            teamSize: 3,
            energyPerRound: 2,
            evolutionPicksPerOpportunity: 2,
            roundCap: 30,
            criticalMultiplier: 2.0,
            firstEvolutionRound: 1,
            evolutionInterval: 2));
    }

    /// <summary>
    /// A format moves the team size and nothing else: the energy, the schedule, the round cap and the
    /// critical multiplier are the game's, not the format's, so a 2v2 is the same rules on another value.
    /// </summary>
    [Fact]
    public void A_format_moves_the_team_size_and_leaves_every_other_value_alone()
    {
        var twoOnTwo = RuleSet.Default.InFormat(MatchFormat.Parse("2v2"));

        twoOnTwo.TeamSize.ShouldBe(2);
        twoOnTwo.Format.ToString().ShouldBe("2v2");
        twoOnTwo.ShouldBe(RuleSet.Create(
            teamSize: 2,
            energyPerRound: RuleSet.Default.EnergyPerRound,
            evolutionPicksPerOpportunity: RuleSet.Default.EvolutionPicksPerOpportunity,
            roundCap: RuleSet.Default.RoundCap,
            criticalMultiplier: RuleSet.Default.CriticalMultiplier,
            firstEvolutionRound: RuleSet.Default.FirstEvolutionRound,
            evolutionInterval: RuleSet.Default.EvolutionInterval));
    }

    [Fact]
    public void The_format_a_rule_set_already_plays_leaves_it_as_it_is() =>
        RuleSet.Default.InFormat(MatchFormat.ThreeOnThree).ShouldBeSameAs(RuleSet.Default);

    /// <summary>
    /// A creature buys at most one package an opportunity (ADR 0066), so a side of one has one pick however
    /// many the allowance names. This is the number to print; the gate counts creatures.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    public void A_side_smaller_than_the_allowance_caps_the_picks_it_can_use(int teamSize, int usable) =>
        RuleSet.Default.InFormat(MatchFormat.OfTeamSize(teamSize)).EvolutionPicksUsableInAnOpportunity.ShouldBe(usable);

    [Fact]
    public void A_team_size_the_engine_cannot_encode_is_refused() =>
        Should.Throw<ArgumentOutOfRangeException>(() => RuleSet.Create(
            teamSize: MatchFormat.MaxTeamSize + 1,
            energyPerRound: 2,
            evolutionPicksPerOpportunity: 2,
            roundCap: 30,
            criticalMultiplier: 2.0));

    /// <summary>
    /// The schedule, which is one question asked of the rule set rather than a rule each caller works out:
    /// two picks at round 1, none at round 2, two at round 3 (ADR 0056).
    /// </summary>
    [Fact]
    public void The_default_schedule_offers_an_opportunity_every_other_round_from_the_first()
    {
        var rules = RuleSet.Default;

        rules.IsEvolutionRound(1).ShouldBeTrue();
        rules.IsEvolutionRound(2).ShouldBeFalse();
        rules.IsEvolutionRound(3).ShouldBeTrue();
        rules.EvolutionPicksIn(1).ShouldBe(2);
        rules.EvolutionPicksIn(2).ShouldBe(0);
        rules.NextEvolutionRound(1).ShouldBe(1);
        rules.NextEvolutionRound(2).ShouldBe(3);
        rules.NextEvolutionRound(3).ShouldBe(3);
    }

    /// <summary>An opportunity that starts later leaves the rounds before it with nothing to spend.</summary>
    [Fact]
    public void A_schedule_that_starts_later_offers_nothing_before_it()
    {
        var rules = RuleSet.Create(3, 2, 1, 30, 2.0, firstEvolutionRound: 4, evolutionInterval: 3);

        rules.IsEvolutionRound(1).ShouldBeFalse();
        rules.IsEvolutionRound(4).ShouldBeTrue();
        rules.IsEvolutionRound(7).ShouldBeTrue();
        rules.NextEvolutionRound(1).ShouldBe(4);
        rules.NextEvolutionRound(5).ShouldBe(7);
        rules.EvolutionPicksIn(4).ShouldBe(1);
    }

    /// <summary>Every round, which is what the prototype played before the packages arrived.</summary>
    [Fact]
    public void An_interval_of_one_offers_every_round()
    {
        var rules = RuleSet.Create(3, 2, 2, 30, 2.0, firstEvolutionRound: 1, evolutionInterval: 1);

        rules.IsEvolutionRound(2).ShouldBeTrue();
        rules.NextEvolutionRound(2).ShouldBe(2);
    }

    [Theory]
    [InlineData(0, 2, 2, 30, 2.0)]
    [InlineData(3, -1, 2, 30, 2.0)]
    [InlineData(3, 2, -1, 30, 2.0)]
    [InlineData(3, 2, 2, 0, 2.0)]
    [InlineData(3, 2, 2, 30, 0.5)]
    [InlineData(3, 2, 2, 30, double.NaN)]
    [InlineData(3, 2, 2, 30, double.PositiveInfinity)]
    public void Invalid_values_are_rejected(int teamSize, int energyPerRound, int picks, int roundCap, double criticalMultiplier)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RuleSet.Create(teamSize, energyPerRound, picks, roundCap, criticalMultiplier));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    public void An_impossible_schedule_is_rejected(int firstEvolutionRound, int evolutionInterval)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RuleSet.Create(3, 2, 2, 30, 2.0, firstEvolutionRound, evolutionInterval));
    }
}

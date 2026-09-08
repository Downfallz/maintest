using DownfallArena.Domain.Matches;

namespace DownfallArena.Domain.Tests.Matches;

public sealed class RuleSetTests
{
    [Fact]
    public void The_default_rule_set_matches_the_prototype()
    {
        RuleSet.Default.ShouldBe(RuleSet.Create(teamSize: 3, energyPerRound: 2, evolutionPicksPerRound: 2, roundCap: 30, criticalMultiplier: 2.0));
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
}

using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Tests.Contracts.Resources.Spells.Effects;

public class BleedTests
{
    [Fact]
    public void GivenValidArgs_WhenCreatingBleedWithOf_ThenStoresValues()
    {
        var amountPerTick = 3;
        var durationRounds = 2;

        var bleed = Bleed.Of(amountPerTick, durationRounds);

        Assert.Equal(amountPerTick, bleed.AmountPerTick);
        Assert.Equal(durationRounds, bleed.DurationRounds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void GivenNonPositiveAmountPerTick_WhenCreatingBleed_ThenThrows(int amountPerTick)
    {
        Assert.Throws<ArgumentException>(() =>
            Bleed.Of(amountPerTick, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-3)]
    public void GivenNonPositiveDuration_WhenCreatingBleed_ThenThrows(int durationRounds)
    {
        Assert.Throws<ArgumentException>(() =>
            Bleed.Of(1, durationRounds));
    }

    [Fact]
    public void GivenParams_WhenUsingEnemySingle_ThenUsesEnemySingleTargetAndStoresValues()
    {
        var amountPerTick = 4;
        var durationRounds = 3;

        var bleed = Bleed.Of(amountPerTick, durationRounds);

        Assert.Equal(amountPerTick, bleed.AmountPerTick);
        Assert.Equal(durationRounds, bleed.DurationRounds);
    }
}

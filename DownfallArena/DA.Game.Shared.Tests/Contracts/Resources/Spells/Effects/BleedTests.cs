using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Tests.Contracts.Resources.Spells.Effects;

public class BleedTests
{
    [Fact]
    public void GivenValidArgs_WhenCreatingBleedWithOf_ThenStoresValues()
    {
        var targeting = TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.SingleTarget, 1);
        var amountPerTick = 3;
        var durationRounds = 2;

        var bleed = Bleed.Of(amountPerTick, durationRounds, targeting);

        Assert.Equal(amountPerTick, bleed.AmountPerTick);
        Assert.Equal(durationRounds, bleed.DurationRounds);
        Assert.Equal(targeting, bleed.Targeting);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void GivenNonPositiveAmountPerTick_WhenCreatingBleed_ThenThrows(int amountPerTick)
    {
        var targeting = TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.SingleTarget, 1);

        Assert.Throws<ArgumentException>(() =>
            Bleed.Of(amountPerTick, 1, targeting));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-3)]
    public void GivenNonPositiveDuration_WhenCreatingBleed_ThenThrows(int durationRounds)
    {
        var targeting = TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.SingleTarget, 1);

        Assert.Throws<ArgumentException>(() =>
            Bleed.Of(1, durationRounds, targeting));
    }

    [Fact]
    public void GivenParams_WhenUsingEnemySingle_ThenUsesEnemySingleTargetAndStoresValues()
    {
        var amountPerTick = 4;
        var durationRounds = 3;

        var bleed = Bleed.EnemySingle(amountPerTick, durationRounds);

        Assert.Equal(amountPerTick, bleed.AmountPerTick);
        Assert.Equal(durationRounds, bleed.DurationRounds);
        Assert.Equal(TargetOrigin.Enemy, bleed.Targeting.Origin);
        Assert.Equal(TargetScope.SingleTarget, bleed.Targeting.Scope);
        Assert.Equal(1, bleed.Targeting.MaxTargets);
    }

    [Fact]
    public void GivenBleed_WhenWithTargeting_ThenKeepsPayloadAndChangesTargeting()
    {
        var originalTargeting = TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.SingleTarget, 1);
        var bleed = Bleed.Of(2, 3, originalTargeting);

        var newTargeting = TargetingSpec.Of(TargetOrigin.Ally, TargetScope.Multi, null);

        var updated = bleed.WithTargeting(newTargeting);

        // Payload immuable
        Assert.Equal(bleed.AmountPerTick, updated.AmountPerTick);
        Assert.Equal(bleed.DurationRounds, updated.DurationRounds);

        // Targeting changé
        Assert.Equal(newTargeting, updated.Targeting);
        Assert.NotEqual(bleed.Targeting, updated.Targeting);

        // Enregistrement immuable : un nouvel objet
        Assert.NotSame(bleed, updated);
    }
}

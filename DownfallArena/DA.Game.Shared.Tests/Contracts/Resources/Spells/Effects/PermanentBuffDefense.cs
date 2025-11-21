using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Tests.Contracts.Resources.Spells.Effects;

public class PermanentBuffDefenseTests
{
    [Fact]
    public void GivenValidArgs_WhenCreatingPermanentBuffDefenseWithOf_ThenStoresValues()
    {
        var targeting = TargetingSpec.Of(TargetOrigin.Ally, TargetScope.SingleTarget, 1);
        var amount = 3;

        var buff = PermanentBuffDefense.Of(amount, targeting);

        Assert.Equal(amount, buff.Amount);
        Assert.Equal(targeting, buff.Targeting);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void GivenNonPositiveAmount_WhenCreatingPermanentBuffDefense_ThenThrows(int amount)
    {
        var targeting = TargetingSpec.Of(TargetOrigin.Ally, TargetScope.SingleTarget, 1);

        Assert.Throws<ArgumentException>(() =>
            PermanentBuffDefense.Of(amount, targeting));
    }

    [Fact]
    public void GivenParams_WhenUsingSelf_ThenUsesSelfSingleTargetPreset()
    {
        var amount = 5;

        var buff = PermanentBuffDefense.Self(amount);

        Assert.Equal(amount, buff.Amount);
        Assert.Equal(TargetOrigin.Self, buff.Targeting.Origin);
        Assert.Equal(TargetScope.SingleTarget, buff.Targeting.Scope);
        Assert.Equal(1, buff.Targeting.MaxTargets);
    }
}

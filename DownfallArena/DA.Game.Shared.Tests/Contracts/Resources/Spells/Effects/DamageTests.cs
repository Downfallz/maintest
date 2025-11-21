using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Tests.Contracts.Resources.Spells.Effects;

public class DamageTests
{
    [Fact]
    public void GivenValidArgs_WhenCreatingDamageWithOf_ThenStoresValues()
    {
        var targeting = TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.SingleTarget, 1);
        var amount = 5;

        var dmg = Damage.Of(amount, targeting);

        Assert.Equal(amount, dmg.Amount);
        Assert.Equal(targeting, dmg.Targeting);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void GivenNonPositiveAmount_WhenCreatingDamage_ThenThrows(int amount)
    {
        var targeting = TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.SingleTarget, 1);

        Assert.Throws<ArgumentException>(() =>
            Damage.Of(amount, targeting));
    }

    [Fact]
    public void GivenParams_WhenUsingSingleTargetEnemy_ThenUsesCorrectPresetTargeting()
    {
        var amount = 7;

        var dmg = Damage.SingleTargetEnemy(amount);

        Assert.Equal(amount, dmg.Amount);
        Assert.Equal(TargetOrigin.Enemy, dmg.Targeting.Origin);
        Assert.Equal(TargetScope.SingleTarget, dmg.Targeting.Scope);
        Assert.Equal(1, dmg.Targeting.MaxTargets);
    }

    [Fact]
    public void GivenDamage_WhenUsingWithTargeting_ThenKeepsPayloadAndChangesTargeting()
    {
        var originalTargeting = TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.SingleTarget, 1);
        var dmg = Damage.Of(10, originalTargeting);

        var newTargeting = TargetingSpec.Of(TargetOrigin.Ally, TargetScope.Multi, null);

        var updated = dmg.WithTargeting(newTargeting);

        // payload immuable
        Assert.Equal(dmg.Amount, updated.Amount);

        // targeting modifié
        Assert.Equal(newTargeting, updated.Targeting);
        Assert.NotEqual(dmg.Targeting, updated.Targeting);

        // immutabilité record
        Assert.NotSame(dmg, updated);
    }
}

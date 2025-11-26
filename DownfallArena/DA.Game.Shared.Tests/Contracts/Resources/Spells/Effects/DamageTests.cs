using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Tests.Contracts.Resources.Spells.Effects;

public class DamageTests
{
    [Fact]
    public void GivenValidArgs_WhenCreatingDamageWithOf_ThenStoresValues()
    {
        var amount = 5;

        var dmg = Damage.Of(amount);

        Assert.Equal(amount, dmg.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void GivenNonPositiveAmount_WhenCreatingDamage_ThenThrows(int amount)
    {
        Assert.Throws<ArgumentException>(() =>
            Damage.Of(amount));
    }
}

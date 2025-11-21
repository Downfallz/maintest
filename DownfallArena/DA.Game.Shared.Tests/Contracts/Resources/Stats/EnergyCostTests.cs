using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Shared.Tests.Contracts.Resources.Stats;

public class EnergyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(27)]
    public void GivenValidValue_WhenCreatingEnergy_ThenStoresValue(int value)
    {
        var energy = Energy.Of(value);

        Assert.Equal(value, energy.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-3)]
    [InlineData(-42)]
    public void GivenNegativeValue_WhenCreatingEnergy_ThenThrows(int value)
    {
        Assert.Throws<ArgumentException>(() => Energy.Of(value));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(4, "4")]
    [InlineData(11, "11")]
    public void GivenEnergy_WhenCallingToString_ThenReturnsRawValue(int value, string expected)
    {
        var energy = Energy.Of(value);

        Assert.Equal(expected, energy.ToString());
    }
}

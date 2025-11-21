using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Shared.Tests.Contracts.Resources.Stats;

public class CriticalChanceTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.99)]
    [InlineData(1.0)]
    public void GivenValidValue_WhenCreatingCriticalChance_ThenStoresValue(double value)
    {
        var crit = CriticalChance.Of(value);

        Assert.Equal(value, crit.Value);
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(-1)]
    [InlineData(-42)]
    [InlineData(1.0000001)]
    [InlineData(2)]
    [InlineData(999)]
    public void GivenOutOfRangeValue_WhenCreatingCriticalChance_ThenThrows(double value)
    {
        Assert.Throws<ArgumentException>(() => CriticalChance.Of(value));
    }

    [Fact]
    public void GivenCriticalChance_WhenCallingToPercent_ThenReturnsValueTimes100()
    {
        var crit = CriticalChance.Of(0.37);

        var percent = crit.ToPercent();

        Assert.Equal(37.0, percent, 3);
    }

    [Theory]
    [InlineData(0.0, "0%")]
    [InlineData(0.125, "12.5%")]
    [InlineData(0.5, "50%")]
    [InlineData(0.3333, "33.33%")]
    [InlineData(1.0, "100%")]
    public void GivenCriticalChance_WhenCallingToString_ThenFormatsAsPercent(double value, string expected)
    {
        var crit = CriticalChance.Of(value);

        Assert.Equal(expected, crit.ToString());
    }
}

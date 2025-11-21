using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Shared.Tests.Contracts.Resources.Stats;

public class DefenseTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(42)]
    public void GivenValidValue_WhenCreatingDefense_ThenStoresValue(int value)
    {
        var def = Defense.Of(value);

        Assert.Equal(value, def.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    [InlineData(-100)]
    public void GivenNegativeValue_WhenCreatingDefense_ThenThrows(int value)
    {
        Assert.Throws<ArgumentException>(() => Defense.Of(value));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(3, "3")]
    [InlineData(15, "15")]
    public void GivenDefense_WhenCallingToString_ThenReturnsRawValue(int value, string expected)
    {
        var def = Defense.Of(value);

        Assert.Equal(expected, def.ToString());
    }
}

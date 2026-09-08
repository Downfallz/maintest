using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Shared.Tests.Contracts.Resources.Stats;

public class InitiativeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void GivenValidValue_WhenCreatingInitiative_ThenStoresValue(int value)
    {
        var init = Initiative.Of(value);

        Assert.Equal(value, init.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    [InlineData(-42)]
    public void GivenNegativeValue_WhenCreatingInitiative_ThenThrows(int value)
    {
        Assert.Throws<ArgumentException>(() => Initiative.Of(value));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(2, "2")]
    [InlineData(15, "15")]
    public void GivenInitiative_WhenCallingToString_ThenReturnsRawValue(int value, string expected)
    {
        var init = Initiative.Of(value);

        Assert.Equal(expected, init.ToString());
    }
}

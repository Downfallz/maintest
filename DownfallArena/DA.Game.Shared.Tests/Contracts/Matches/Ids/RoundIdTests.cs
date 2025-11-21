using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Shared.Tests.Contracts.Matches.Ids;

public class RoundIdTests
{
    [Fact]
    public void GivenValue_WhenCreatingRoundId_ThenStoresValue()
    {
        var value = 3;

        var id = RoundId.New(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void GivenRoundId_WhenToString_ThenReturnsValueAsString()
    {
        var id = RoundId.New(5);

        var s = id.ToString();

        Assert.Equal("5", s);
    }

    [Fact]
    public void GivenTwoRoundIdsWithSameValue_WhenComparing_ThenTheyAreEqual()
    {
        var a = RoundId.New(10);
        var b = RoundId.New(10);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void GivenTwoRoundIdsWithDifferentValues_WhenComparing_ThenTheyAreNotEqual()
    {
        var a = RoundId.New(1);
        var b = RoundId.New(2);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void GivenRoundId_WhenCallingNext_ThenReturnsNextRoundId()
    {
        var id = RoundId.New(3);

        var next = id.Next();

        Assert.Equal(4, next.Value);
        Assert.Equal(3, id.Value); // immutability
    }

    [Fact]
    public void GivenFirst_WhenAccessingFirst_ThenValueIsOne()
    {
        var first = RoundId.First;

        Assert.Equal(1, first.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void GivenNonPositiveValue_WhenCreatingRoundId_ThenThrows(int value)
    {
        var act = () => { RoundId.New(value); };

        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
}

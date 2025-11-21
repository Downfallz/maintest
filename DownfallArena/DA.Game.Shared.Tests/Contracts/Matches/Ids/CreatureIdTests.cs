using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Shared.Tests.Contracts.Matches.Ids;

public class CreatureIdTests
{
    [Fact]
    public void GivenValue_WhenCreatingCreatureId_ThenStoresValue()
    {
        var value = 42;

        var id = CreatureId.New(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void GivenCreatureId_WhenToString_ThenReturnsValueAsString()
    {
        var id = CreatureId.New(7);

        var s = id.ToString();

        Assert.Equal("7", s);
    }

    [Fact]
    public void GivenTwoCreatureIdsWithSameValue_WhenComparing_ThenTheyAreEqual()
    {
        var a = CreatureId.New(10);
        var b = CreatureId.New(10);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void GivenTwoCreatureIdsWithDifferentValues_WhenComparing_ThenTheyAreNotEqual()
    {
        var a = CreatureId.New(1);
        var b = CreatureId.New(2);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void GivenNonPositiveValue_WhenCreatingCreatureId_ThenThrows(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatureId.New(value));
    }
}

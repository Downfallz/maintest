using DA.Game.Shared.Contracts.Players.Ids;

namespace DA.Game.Shared.Tests.Contracts.Players.Ids;

public class PlayerIdTests
{
    [Fact]
    public void GivenNew_WhenCreatingPlayerId_ThenValueIsNotEmpty()
    {
        var id = PlayerId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void GivenValidGuid_WhenCreatingPlayerId_ThenStoresValue()
    {
        var g = Guid.NewGuid();

        var id = PlayerId.Of(g);

        Assert.Equal(g, id.Value);
    }

    [Fact]
    public void GivenPlayerId_WhenToString_ThenReturnsGuidAsString()
    {
        var g = Guid.NewGuid();
        var id = PlayerId.Of(g);

        var s = id.ToString();

        Assert.Equal(g.ToString(), s);
    }

    [Fact]
    public void GivenTwoPlayerIdsWithSameGuid_WhenComparing_ThenTheyAreEqual()
    {
        var g = Guid.NewGuid();

        var a = PlayerId.Of(g);
        var b = PlayerId.Of(g);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void GivenTwoPlayerIdsWithDifferentGuids_WhenComparing_ThenTheyAreNotEqual()
    {
        var a = PlayerId.New();
        var b = PlayerId.New();

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void GivenEmptyGuid_WhenCreatingPlayerId_ThenThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlayerId.Of(Guid.Empty)
        );
    }
}

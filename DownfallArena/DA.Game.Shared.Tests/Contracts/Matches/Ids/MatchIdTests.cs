using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Shared.Tests.Contracts.Matches.Ids;

public class MatchIdTests
{
    [Fact]
    public void GivenNew_WhenCreatingMatchId_ThenValueIsNotEmpty()
    {
        var id = MatchId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void GivenGuid_WhenCreatingMatchId_ThenStoresValue()
    {
        var g = Guid.NewGuid();

        var id = MatchId.Of(g);

        Assert.Equal(g, id.Value);
    }

    [Fact]
    public void GivenMatchId_WhenToString_ThenReturnsGuidAsString()
    {
        var g = Guid.NewGuid();
        var id = MatchId.Of(g);

        var s = id.ToString();

        Assert.Equal(g.ToString(), s);
    }

    [Fact]
    public void GivenTwoMatchIdsWithSameGuid_WhenComparing_ThenTheyAreEqual()
    {
        var g = Guid.NewGuid();

        var a = MatchId.Of(g);
        var b = MatchId.Of(g);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void GivenTwoMatchIdsWithDifferentGuids_WhenComparing_ThenTheyAreNotEqual()
    {
        var a = MatchId.New();
        var b = MatchId.New();

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void GivenEmptyGuid_WhenCreatingMatchId_ThenThrows()
    {
        var act = () => { MatchId.Of(Guid.Empty); };

        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
}

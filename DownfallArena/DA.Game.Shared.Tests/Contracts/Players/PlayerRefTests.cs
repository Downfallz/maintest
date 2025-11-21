using DA.Game.Shared.Contracts.Players;
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Contracts.Players.Ids;

namespace DA.Game.Shared.Tests.Contracts.Players;

public class PlayerRefTests
{
    [Fact]
    public void GivenValidInputs_WhenCreatingPlayerRef_ThenStoresValues()
    {
        var id = PlayerId.New();
        var kind = ActorKind.Human;
        var name = "Alice";

        var player = PlayerRef.Create(id, kind, name);

        Assert.Equal(id, player.Id);
        Assert.Equal(kind, player.Kind);
        Assert.Equal(name, player.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GivenInvalidDisplayName_WhenCreatingPlayerRef_ThenThrows(string value)
    {
        var id = PlayerId.New();
        var kind = ActorKind.Bot;

        Assert.Throws<ArgumentException>(() => PlayerRef.Create(id, kind, value));
    }

    [Fact]
    public void GivenTwoEqualPlayerRefs_WhenComparing_ThenTheyAreEqual()
    {
        var id = PlayerId.New();
        var kind = ActorKind.Human;

        var a = PlayerRef.Create(id, kind, "Bob");
        var b = PlayerRef.Create(id, kind, "Bob");

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void GivenTwoDifferentPlayerRefs_WhenComparing_ThenTheyAreNotEqual()
    {
        var a = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "A");
        var b = PlayerRef.Create(PlayerId.New(), ActorKind.Human, "B");

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }
}

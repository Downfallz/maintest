using DA.Game.Domain2.Players.Entities;
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Contracts.Players.Ids;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Tests.Players.Entities;

public class PlayerTests
{
    [Theory]
    [InlineData("Marc", ActorKind.Human)]
    [InlineData("AI Bot", ActorKind.Bot)]
    public void GivenValidArgs_WhenCreatingPlayer_ThenStoresValues(
        string name,
        ActorKind kind)
    {
        var id = PlayerId.New();

        var p = Player.Create(id, name, kind);

        Assert.Equal(id, p.Id);
        Assert.Equal(name, p.Name);
        Assert.Equal(kind, p.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GivenInvalidName_WhenCreatingPlayer_ThenThrows(string name)
    {
        var id = PlayerId.New();

        Assert.Throws<ArgumentException>(() =>
            Player.Create(id, name, ActorKind.Human));
    }

    [Fact]
    public void GivenPlayer_WhenToString_ThenReturnsFormattedString()
    {
        var id = PlayerId.New();
        var p = Player.Create(id, "Marc", ActorKind.Human);

        Assert.Equal("Marc (Human)", p.ToString());
    }
}

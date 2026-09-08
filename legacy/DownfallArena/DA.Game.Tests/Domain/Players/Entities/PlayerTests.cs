namespace DA.Game.Tests.Domain.Players.Entities;

using DA.Game.Domain2.Players.Entities;
using DA.Game.Domain2.Players.Messages;
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Contracts.Players.Ids;
using System;
using Xunit;


public class PlayerTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var id = PlayerId.New();
        var name = "TestPlayer";
        var kind = ActorKind.Human;

        // Act
        var player = new Player(id, name, kind);

        // Assert
        Assert.Equal(id, player.Id);
        Assert.Equal(name, player.Name);
        Assert.Equal(kind, player.Kind);
    }

    [Fact]
    public void Rename_WithValidName_UpdatesNameAndReturnsSuccess()
    {
        // Arrange
        var player = new Player(PlayerId.New(), "OldName", ActorKind.Bot);
        var newName = "NewName";

        // Act
        var result = player.Rename(newName);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(newName, player.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rename_WithInvalidName_ReturnsFailureAndDoesNotChangeName(string invalidName)
    {
        // Arrange
        var originalName = "Original";
        var player = new Player(PlayerId.New(), originalName, ActorKind.Human);

        // Act
        var result = player.Rename(invalidName);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(PlayerErrors.InvalidName, result.Error);
        Assert.Equal(originalName, player.Name);
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        var player = new Player(PlayerId.New(), "PlayerX", ActorKind.Bot);

        // Act
        var str = player.ToString();

        // Assert
        Assert.Equal("PlayerX (Bot)", str);
    }
}
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using FluentAssertions;
using Xunit;

namespace DA.Game.Domain2.Tests.Matches.ValueObjects.Planning;

public sealed class SpeedChoiceTests
{
    [Fact]
    public void Of_WhenCreatureIdIsDefault_ShouldThrow()
    {
        // Arrange
        var id = default(CreatureId); // Value = 0

        // Act
        var act = () => SpeedChoice.Of(id, SkillSpeed.Quick);

        // Assert
        act.Should()
           .Throw<ArgumentException>()
           .Which.ParamName.Should().Be("creatureId");
    }


    [Fact]
    public void Of_ShouldAssignProperties()
    {
        // Arrange
        var id = new CreatureId(42);

        // Act
        var sc = SpeedChoice.Of(id, SkillSpeed.Standard);

        // Assert
        sc.CreatureId.Should().Be(id);
        sc.Speed.Should().Be(SkillSpeed.Standard);
    }

    [Fact]
    public void TwoInstances_WithSameValues_ShouldBeEqualValueObjects()
    {
        // Arrange
        var id = new CreatureId(10);

        var a = SpeedChoice.Of(id, SkillSpeed.Quick);
        var b = SpeedChoice.Of(id, SkillSpeed.Quick);

        // Assert
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void ToString_ShouldContainCreatureIdAndSpeed()
    {
        // Arrange
        var id = new CreatureId(99);
        var sc = SpeedChoice.Of(id, SkillSpeed.Quick);

        // Act
        var text = sc.ToString();

        // Assert
        text.Should().Contain("SpeedChoice");
        text.Should().Contain("99");
        text.Should().Contain("Quick");
    }
}

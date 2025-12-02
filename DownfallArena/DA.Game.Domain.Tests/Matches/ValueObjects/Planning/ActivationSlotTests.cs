using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Stats;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Tests.Matches.ValueObjects.Planning;

public sealed class ActivationSlotTests
{
    [Fact]
    public void Constructor_ShouldSet_AllProperties()
    {
        // Arrange
        var owner = PlayerSlot.Player1;
        var creatureId = new CreatureId(42);
        var speed = SkillSpeed.Quick;
        var initiative = Initiative.Of(7);

        // Act
        var slot = new ActivationSlot(owner, creatureId, speed, initiative);

        // Assert
        slot.Owner.Should().Be(owner);
        slot.CreatureId.Should().Be(creatureId);
        slot.Speed.Should().Be(speed);
        slot.Initiative.Should().Be(initiative);
    }

    [Fact]
    public void Equality_ShouldBeBasedOnAllValues()
    {
        // Arrange
        var id = new CreatureId(1);
        var initiative = Initiative.Of(5);

        var s1 = new ActivationSlot(PlayerSlot.Player1, id, SkillSpeed.Standard, initiative);
        var s2 = new ActivationSlot(PlayerSlot.Player1, id, SkillSpeed.Standard, initiative);
        var s3 = new ActivationSlot(PlayerSlot.Player2, id, SkillSpeed.Standard, initiative);

        // Assert
        s1.Should().Be(s2);
        s1.Should().NotBe(s3);
    }

    [Fact]
    public void ToString_ShouldReturnReadableRepresentation()
    {
        // Arrange
        var owner = PlayerSlot.Player2;
        var creatureId = new CreatureId(99);
        var speed = SkillSpeed.Quick;
        var initiative = Initiative.Of(10);

        var slot = new ActivationSlot(owner, creatureId, speed, initiative);

        // Act
        var text = slot.ToString();

        // Assert
        text.Should().Contain(owner.ToString());
        text.Should().Contain(creatureId.Value.ToString());
        text.Should().Contain(speed.ToString());
        text.Should().Contain("Init=10");
    }
}
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Stats;
using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.ValueObjects.Planning;

public sealed class CombatTimelineTests
{
    private static ActivationSlot CreateSlot(int creatureId, SkillSpeed speed, int initiative)
    {
        return new ActivationSlot(
            Owner: PlayerSlot.Player1,
            CreatureId: new CreatureId(creatureId),
            Speed: speed,
            Initiative: Initiative.Of(initiative));
    }

    [Fact]
    public void Empty_ShouldExposeZeroCount_AndNoSlots()
    {
        // Act
        var timeline = CombatTimeline.Empty;

        // Assert
        timeline.Should().NotBeNull();
        timeline.Count.Should().Be(0);
        timeline.Slots.Should().NotBeNull();
        timeline.Slots.Should().BeEmpty();
        timeline.ToString().Should().Contain("0 slots");
    }

    [Fact]
    public void FromSlots_WhenSlotsIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        IEnumerable<ActivationSlot> slots = null!;

        // Act
        var act = () => CombatTimeline.FromSlots(slots);

        // Assert
        act.Should()
           .Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("slots");
    }

    [Fact]
    public void FromSlots_ShouldMaterializeAndExposeSameOrder()
    {
        // Arrange
        var s1 = CreateSlot(1, SkillSpeed.Quick, 10);
        var s2 = CreateSlot(2, SkillSpeed.Standard, 5);
        var input = new List<ActivationSlot> { s1, s2 };

        // Act
        var timeline = CombatTimeline.FromSlots(input);

        // Assert
        timeline.Count.Should().Be(2);
        timeline[0].Should().Be(s1);
        timeline[1].Should().Be(s2);

        // Mutate input list to ensure timeline is not affected
        input.Clear();
        timeline.Count.Should().Be(2);
        timeline.Slots.Should().HaveCount(2);
    }

    [Fact]
    public void Indexer_ShouldReturnSlotAtGivenIndex()
    {
        // Arrange
        var s1 = CreateSlot(1, SkillSpeed.Quick, 7);
        var s2 = CreateSlot(2, SkillSpeed.Standard, 3);
        var timeline = CombatTimeline.FromSlots(new[] { s1, s2 });

        // Act
        var first = timeline[0];
        var second = timeline[1];

        // Assert
        first.Should().Be(s1);
        second.Should().Be(s2);
    }

    [Fact]
    public void ToString_ShouldContainCount()
    {
        // Arrange
        var s1 = CreateSlot(1, SkillSpeed.Quick, 7);
        var timeline = CombatTimeline.FromSlots(new[] { s1 });

        // Act
        var text = timeline.ToString();

        // Assert
        text.Should().Contain("1 slots");
    }
}

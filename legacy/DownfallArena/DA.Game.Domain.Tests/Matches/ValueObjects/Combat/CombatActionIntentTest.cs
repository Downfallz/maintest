using System;
using AutoFixture.Xunit2;
using DA.Game.Domain.Tests; // MatchAutoDataAttribute
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using FluentAssertions;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.ValueObjects.Combat;

public sealed class CombatActionIntentTests
{
    [Theory]
    [MatchAutoData]
    public void Create_ShouldCreateValidIntent(
        CreatureId creatureId,
        Spell spell)
    {
        // Arrange
        // (creatureId may be 0 or negative from AutoFixture, so we clamp)
        var validId = new CreatureId(
            creatureId.Value <= 0 ? 1 : creatureId.Value);

        // Act
        var intent = CombatActionIntent.Create(validId, spell);

        // Assert
        intent.ActorId.Should().Be(validId);
        intent.SpellRef.Should().Be(spell);
    }

    [Theory]
    [MatchAutoData]
    public void Create_WhenSpellIsNull_ShouldThrow(
        CreatureId creatureId)
    {
        // Arrange
        var validId = new CreatureId(
            creatureId.Value <= 0 ? 1 : creatureId.Value);

        // Act
        Action act = () => CombatActionIntent.Create(validId, null!);

        // Assert
        act.Should()
           .Throw<ArgumentNullException>()
           .WithMessage("*I702*");
    }

    [Theory]
    [MatchAutoData]
    public void Create_WhenCreatureIdIsInvalid_ShouldThrow(
        Spell spell)
    {
        // Arrange
        var invalidId = new CreatureId(0);

        // Act
        Action act = () => CombatActionIntent.Create(invalidId, spell);

        // Assert
        act.Should()
           .Throw<ArgumentException>()
           .WithMessage("*I701*");
    }

    [Theory]
    [MatchAutoData]
    public void RecordEquality_ShouldBeValueBased(
        CreatureId creatureId,
        Spell spell)
    {
        // Arrange
        var validId = new CreatureId(
            creatureId.Value <= 0 ? 1 : creatureId.Value);

        var a = CombatActionIntent.Create(validId, spell);
        var b = CombatActionIntent.Create(validId, spell);

        // Act & Assert
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }
}

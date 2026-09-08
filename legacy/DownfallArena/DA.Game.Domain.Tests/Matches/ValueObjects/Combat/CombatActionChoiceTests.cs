using System;
using System.Collections.Generic;
using AutoFixture.Xunit2;
using DA.Game.Domain.Tests; // MatchAutoDataAttribute
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using FluentAssertions;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.ValueObjects.Combat;

public sealed class CombatActionChoiceTests
{
    [Theory]
    [MatchAutoData]
    public void Create_ShouldCreateValidChoice(
        CreatureId creatureId,
        Spell spell,
        List<CreatureId> targets)
    {
        // Arrange
        var validId = new CreatureId(
            creatureId.Value <= 0 ? 1 : creatureId.Value);

        targets ??= new List<CreatureId> { validId };

        // Act
        var choice = CombatActionChoice.Create(validId, spell, targets);

        // Assert
        choice.ActorId.Should().Be(validId);
        choice.SpellRef.Should().Be(spell);
        choice.TargetIds.Should().BeEquivalentTo(targets);
    }

    [Theory]
    [MatchAutoData]
    public void Create_WhenSpellIsNull_ShouldThrow(
        CreatureId creatureId,
        List<CreatureId> targets)
    {
        // Arrange
        var validId = new CreatureId(
            creatureId.Value <= 0 ? 1 : creatureId.Value);

        targets ??= new List<CreatureId> { validId };

        // Act
        Action act = () => CombatActionChoice.Create(validId, null!, targets);

        // Assert
        act.Should()
           .Throw<ArgumentNullException>()
           .WithMessage("*I712*");
    }

    [Theory]
    [MatchAutoData]
    public void Create_WhenTargetIdsIsNull_ShouldThrow(
        CreatureId creatureId,
        Spell spell)
    {
        // Arrange
        var validId = new CreatureId(
            creatureId.Value <= 0 ? 1 : creatureId.Value);

        // Act
        Action act = () => CombatActionChoice.Create(validId, spell, null!);

        // Assert
        act.Should()
           .Throw<ArgumentNullException>()
           .WithMessage("*I713*");
    }

    [Theory]
    [MatchAutoData]
    public void Create_WhenActorIdInvalid_ShouldThrow(
        Spell spell,
        List<CreatureId> targets)
    {
        // Arrange
        var invalidId = new CreatureId(0);
        targets ??= new List<CreatureId> { new CreatureId(1) };

        // Act
        Action act = () => CombatActionChoice.Create(invalidId, spell, targets);

        // Assert
        act.Should()
           .Throw<ArgumentException>()
           .WithMessage("*I711*");
    }

    [Theory]
    [MatchAutoData]
    public void FromIntentAndTargets_ShouldReuseIntentActorAndSpell(
        CreatureId creatureId,
        Spell spell,
        List<CreatureId> targets)
    {
        // Arrange
        var validId = new CreatureId(
            creatureId.Value <= 0 ? 1 : creatureId.Value);

        targets ??= new List<CreatureId> { validId };

        var intent = CombatActionIntent.Create(validId, spell);

        // Act
        var choice = CombatActionChoice.FromIntentAndTargets(intent, targets);

        // Assert
        choice.ActorId.Should().Be(intent.ActorId);
        choice.SpellRef.Should().Be(intent.SpellRef);
        choice.TargetIds.Should().BeEquivalentTo(targets);
    }

    [Theory]
    [MatchAutoData]
    public void FromIntentAndTargets_WhenIntentIsNull_ShouldThrow(
        List<CreatureId> targets)
    {
        // Arrange
        targets ??= new List<CreatureId> { new CreatureId(1) };

        // Act
        Action act = () => CombatActionChoice.FromIntentAndTargets(null!, targets);

        // Assert
        act.Should()
           .Throw<ArgumentNullException>()
           .Where(e => e.ParamName == "intent");
    }

    [Theory]
    [MatchAutoData]
    public void FromIntentAndTargets_WhenTargetsNull_ShouldThrow(
        CreatureId creatureId,
        Spell spell)
    {
        // Arrange
        var validId = new CreatureId(
            creatureId.Value <= 0 ? 1 : creatureId.Value);

        var intent = CombatActionIntent.Create(validId, spell);

        // Act
        Action act = () => CombatActionChoice.FromIntentAndTargets(intent, null!);

        // Assert
        act.Should()
           .Throw<ArgumentNullException>()
           .WithMessage("*I713*");
    }

    [Theory]
    [MatchAutoData]
    public void RecordEquality_ShouldBeValueBased(
    CreatureId creatureId,
    Spell spell,
    List<CreatureId> targets)
    {
        // Arrange
        var validId = new CreatureId(
            creatureId.Value <= 0 ? 1 : creatureId.Value);

        targets ??= new List<CreatureId> { validId };

        // Use the same list instance for both records
        var a = CombatActionChoice.Create(validId, spell, targets);
        var b = CombatActionChoice.Create(validId, spell, targets);

        // Act & Assert
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }
}

using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Contracts.Resources.Stats;
using FluentAssertions;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.ValueObjects;

public class CombatActionResultTests
{
    private static Spell CreateFakeSpell()
    {
        var effects = new IEffect[]
        {
            Damage.Of(1)
        };

        return Spell.Create(
            id: new SpellId("spell:test"),
            name: "Test",
            spellType: SpellType.Offensive,
            classType: CreatureClass.Creature,
            initiative: Initiative.Of(0),
            energyCost: Energy.Of(0),
            critChance: CriticalChance.Of(0),
            targetingSpec: TargetingSpec.Of(TargetOrigin.Any, TargetScope.Multi),
            effects: effects);
    }

    [Fact]
    public void Constructor_WhenInstantEffectsIsNull_ShouldSetEmptyList()
    {
        // Arrange
        var spell = CreateFakeSpell();
        var choice = new CombatActionChoice(
            ActorId: new CreatureId(1),
            SpellRef: spell,
            TargetIds: Array.Empty<CreatureId>());

        // Act
        var result = new CombatActionResult(
            originalChoice: choice,
            effectiveChoice: choice,
            instantEffects: null,
            critical: CritComputationResult.Normal(0.0, 0.0),
            targetingFailures: Array.Empty<TargetingFailure>());

        // Assert
        result.InstantEffects.Should().NotBeNull();
        result.InstantEffects.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WhenTargetingFailuresIsNull_ShouldSetEmptyList()
    {
        // Arrange
        var spell = CreateFakeSpell();
        var choice = new CombatActionChoice(
            ActorId: new CreatureId(1),
            SpellRef: spell,
            TargetIds: Array.Empty<CreatureId>());

        // Act
        var result = new CombatActionResult(
            originalChoice: choice,
            effectiveChoice: choice,
            instantEffects: Array.Empty<InstantEffectApplication>(),
            critical: CritComputationResult.Normal(0.0, 0.0),
            targetingFailures: null);

        // Assert
        result.TargetingFailures.Should().NotBeNull();
        result.TargetingFailures.Should().BeEmpty();
        result.HasPartialTargetFailures.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenCriticalIsNull_ShouldUseDefaultNormalCriticalResult()
    {
        // Arrange
        var spell = CreateFakeSpell();
        var choice = new CombatActionChoice(
            ActorId: new CreatureId(1),
            SpellRef: spell,
            TargetIds: Array.Empty<CreatureId>());

        // Act
        var result = new CombatActionResult(
            originalChoice: choice,
            effectiveChoice: choice,
            instantEffects: Array.Empty<InstantEffectApplication>(),
            critical: null!,
            targetingFailures: Array.Empty<TargetingFailure>());

        // Assert
        result.Critical.Should().NotBeNull();
        result.Critical.IsCritical.Should().BeFalse();
        result.Critical.ChanceUsed.Should().Be(0.0);
        result.Critical.Roll.Should().Be(0.0);
        result.Critical.Multiplier.Should().Be(1.0);
    }

    [Fact]
    public void HasPartialTargetFailures_WhenFailuresPresent_ShouldBeTrue()
    {
        // Arrange
        var spell = CreateFakeSpell();
        var choice = new CombatActionChoice(
            ActorId: new CreatureId(1),
            SpellRef: spell,
            TargetIds: Array.Empty<CreatureId>());

        var failures = new[]
        {
            new TargetingFailure(
                TargetId: new CreatureId(2),
                ErrorCode: "D405",
                Message: "All targets must be allies.")
        };

        // Act
        var result = new CombatActionResult(
            originalChoice: choice,
            effectiveChoice: choice,
            instantEffects: Array.Empty<InstantEffectApplication>(),
            critical: CritComputationResult.Normal(0.0, 0.0),
            targetingFailures: failures);

        // Assert
        result.TargetingFailures.Should().HaveCount(1);
        result.HasPartialTargetFailures.Should().BeTrue();
    }
}

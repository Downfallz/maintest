using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Contracts.Resources.Stats;
using DA.Game.Shared.Tests;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.Policies;

public sealed class CostPolicyV1Tests
{
    // --------------------------
    // Guard clauses
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCreatureHasEnoughEnergy_WhenContextIsNull_ThrowsArgumentNullException(
        CombatActionChoice choice,
        CostPolicyV1 sut)
    {
        CreaturePerspective ctx = null!;

        var act = () => sut.EnsureCreatureHasEnoughEnergy(ctx, choice);

        act.Should()
            .Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("ctx");
    }

    [Theory, MatchAutoData]
    public void EnsureCreatureHasEnoughEnergy_WhenChoiceIsNull_ThrowsArgumentNullException(
        CreaturePerspective ctx,
        CostPolicyV1 sut)
    {
        CombatActionChoice choice = null!;

        var act = () => sut.EnsureCreatureHasEnoughEnergy(ctx, choice);

        act.Should()
            .Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("choice");
    }

    // --------------------------
    // Domain behavior (Dxxx)
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCreatureHasEnoughEnergy_WhenActorHasEnoughEnergy_Succeeds(
        CreaturePerspective baseCtx,
        CostPolicyV1 sut)
    {
        // Arrange
        // Spell with fixed cost
        var cost = Energy.Of(5);
        var spell = CreateSpellWithCost(cost);

        // Ensure actor has MORE energy than cost
        var boostedActor = CloneUtility.CloneSnapshot(
            baseCtx.Actor,
            energy: Energy.Of(cost.Value + 1));

        var newCreatures = baseCtx.Creatures
            .Select(c => c.CharacterId == baseCtx.ActorId ? boostedActor : c)
            .ToList();

        var ctx = baseCtx with { Creatures = newCreatures };

        var choice = new CombatActionChoice(
            ActorId: ctx.ActorId,
            SpellRef: spell,
            TargetIds: Array.Empty<CreatureId>());

        // Act
        var result = sut.EnsureCreatureHasEnoughEnergy(ctx, choice);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Theory, MatchAutoData]
    public void EnsureCreatureHasEnoughEnergy_WhenActorDoesNotHaveEnoughEnergy_ReturnsD301(
        CreaturePerspective baseCtx,
        CostPolicyV1 sut)
    {
        // Arrange
        var cost = Energy.Of(5);
        var spell = CreateSpellWithCost(cost);

        // Actor energy BELOW cost
        var lowEnergy = Energy.Of(2);

        var weakenedActor = CloneUtility.CloneSnapshot(
            baseCtx.Actor,
            energy: lowEnergy);

        var newCreatures = baseCtx.Creatures
            .Select(c => c.CharacterId == baseCtx.ActorId ? weakenedActor : c)
            .ToList();

        var ctx = baseCtx with { Creatures = newCreatures };

        var choice = new CombatActionChoice(
            ActorId: ctx.ActorId,
            SpellRef: spell,
            TargetIds: Array.Empty<CreatureId>());

        // Act
        var result = sut.EnsureCreatureHasEnoughEnergy(ctx, choice);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().Be("D301 - Actor does not have enough energy to perform this combat action.");
    }

    // --------------------------
    // Helpers
    // --------------------------

    private static Spell CreateSpellWithCost(Energy cost)
    {
        // Minimal valid spell: 1 damage effect, simple single-target spec
        var effects = new IEffect[]
        {
            Damage.Of(1)
        };

        return Spell.Create(
            id: new SpellId("spell:test:cost:v1"),
            name: "Test Cost Spell",
            spellType: SpellType.Offensive,
            classType: CreatureClass.Creature,
            initiative: Initiative.Of(0),
            energyCost: cost,
            critChance: CriticalChance.Of(0),
            targetingSpec: TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.SingleTarget),
            effects: effects
        );
    }
}
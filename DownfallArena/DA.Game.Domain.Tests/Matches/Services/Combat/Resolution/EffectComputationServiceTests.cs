using DA.Game.Domain2.Matches.Services.Combat;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Contracts.Resources.Stats;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Tests.Matches.Services.Combat.Resolution;

public class EffectComputationServiceV1Tests
{
    private readonly EffectComputationServiceV1 _service = new();

    [Fact]
    public void ComputeRawEffects_WhenNoTargets_ReturnsEmptyBundle()
    {
        // Arrange
        var actorId = new CreatureId(1);
        var spell = CreateSpellWithEffects(Array.Empty<IEffect>());
        var intent = new CombatActionChoice(actorId, spell, Array.Empty<CreatureId>());

        // Act
        var result = _service.ComputeRawEffects(intent);

        // Assert
        result.Should().Be(RawEffectBundle.Empty);
    }

    [Fact]
    public void ComputeRawEffects_WithDamageEffect_CreatesInstantApplicationsPerTarget()
    {
        // Arrange
        var actorId = new CreatureId(1);
        var targets = new[]
        {
            new CreatureId(2),
            new CreatureId(3)
        };

        var damage = Damage.SingleTargetEnemy(10);
        var spell = CreateSpellWithEffects(new IEffect[] { damage });
        var intent = new CombatActionChoice(actorId, spell, targets);

        // Act
        var result = _service.ComputeRawEffects(intent);

        // Assert
        result.InstantEffects.Should().HaveCount(2);
        result.Conditions.Should().BeEmpty();

        var apps = result.InstantEffects.ToArray();
        apps.Select(a => a.TargetId).Should().BeEquivalentTo(targets);
        apps.All(a => a.ActorId == actorId).Should().BeTrue();
        apps.All(a => a.Amount == damage.Amount).Should().BeTrue();
        apps.All(a => a.Kind == damage.Kind).Should().BeTrue();
    }

    [Fact]
    public void ComputeRawEffects_WithBleedEffect_CreatesConditionApplicationsPerTarget()
    {
        // Arrange
        var actorId = new CreatureId(1);
        var targets = new[]
        {
            new CreatureId(2),
            new CreatureId(3)
        };

        // TODO: ajuste la factory de Bleed à ta vraie signature
        var bleed = Bleed.Of(amountPerTick: 2, durationRounds: 3, targeting: TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.SingleTarget, 1));
        var spell = CreateSpellWithEffects(new IEffect[] { bleed });
        var intent = new CombatActionChoice(actorId, spell, targets);

        // Act
        var result = _service.ComputeRawEffects(intent);

        // Assert
        result.InstantEffects.Should().BeEmpty();
        result.Conditions.Should().HaveCount(2);

        var apps = result.Conditions.ToArray();
        apps.Select(a => a.TargetId).Should().BeEquivalentTo(targets);
    }


    // ---------- Helpers ----------

    private static Spell CreateSpellWithEffects(IReadOnlyCollection<IEffect> effects)
    {
        // TODO: adapte à ton vrai constructor / factory Spell.Create(...)
        return Spell.Create(
            id: new SpellId("spell:test:v1"),
            name: "Test Spell",
            spellType: SpellType.Offensive,
            classType: CreatureClass.Creature,
            initiative: Initiative.Of(0),
            energyCost: Energy.Of(0),
            critChance: CriticalChance.Of(0),
            effects: effects
        );
    }

}
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Stats;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Tests.Entities;

public class CombatCreatureTests
{
    [Fact]
    public void FromCreatureTemplate_ShouldInitializeBaseAndCurrentStatsCorrectly()
    {
        // Arrange
        var id = new CreatureId();
        var baseHp = Health.Of(20);
        var baseEnergy = Energy.Of(5);
        var baseDefense = Defense.Of(2);
        var baseInitiative = Initiative.Of(10);
        var baseCrit = CriticalChance.Of(.05);
        var spells = new[] { new SpellId("fireball"), new SpellId("shield") };

        var template = CreateTemplate(
            baseHp,
            baseEnergy,
            baseDefense,
            baseInitiative,
            baseCrit,
            spells
        );

        // Act
        var creature = CombatCreature.FromCreatureTemplate(template, id);

        // Assert
        creature.Id.Should().Be(id);

        creature.BaseHealth.Should().Be(baseHp);
        creature.BaseEnergy.Should().Be(baseEnergy);
        creature.BaseDefense.Should().Be(baseDefense);
        creature.BaseInitiative.Should().Be(baseInitiative);
        creature.BaseCritical.Should().Be(baseCrit);

        creature.Health.Should().Be(baseHp);
        creature.Energy.Should().Be(baseEnergy);
        creature.CurrentInitiative.Should().Be(baseInitiative);

        creature.BonusDefense.Value.Should().Be(0);
        creature.BonusCritical.Value.Should().Be(0);

        creature.StartingSpellIds.Should().BeEquivalentTo(spells);
        creature.IsStunned.Should().BeFalse();
        creature.IsDead.Should().BeFalse();
        creature.IsAlive.Should().BeTrue();
    }

    [Fact]
    public void TakeDamage_WhenAlive_ShouldReduceHealthAndCanKill()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 10);

        // Act
        creature.TakeDamage(4);

        // Assert
        creature.Health.Value.Should().Be(6);
        creature.IsDead.Should().BeFalse();

        // Act 2 - lethal
        creature.TakeDamage(10);

        // Assert 2
        creature.Health.Value.Should().Be(0);
        creature.IsDead.Should().BeTrue();
        creature.IsAlive.Should().BeFalse();
    }

    [Fact]
    public void TakeDamage_WhenDead_ShouldDoNothing()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 5);
        creature.TakeDamage(10); // now dead
        var healthAfterDeath = creature.Health;

        // Act
        creature.TakeDamage(3);

        // Assert
        creature.Health.Should().Be(healthAfterDeath);
    }

    [Fact]
    public void Heal_WhenAlive_ShouldIncreaseHealth()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 10);
        creature.TakeDamage(6); // health = 4

        // Act
        creature.Heal(3);

        // Assert
        creature.Health.Value.Should().Be(7);
        creature.IsDead.Should().BeFalse();
    }

    [Fact]
    public void Heal_WhenDead_ShouldDoNothing()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 5);
        creature.TakeDamage(10); // dead
        var healthAfterDeath = creature.Health;

        // Act
        creature.Heal(10);

        // Assert
        creature.Health.Should().Be(healthAfterDeath);
        creature.IsDead.Should().BeTrue();
    }

    [Fact]
    public void SpendOrLoseEnergy_WhenAlive_ShouldReduceEnergy()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 10, energy: 5);

        // Act
        creature.SpendOrLoseEnergy(3);

        // Assert
        creature.Energy.Value.Should().Be(2);
    }

    [Fact]
    public void SpendOrLoseEnergy_WhenDead_ShouldDoNothing()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 5, energy: 5);
        creature.TakeDamage(10); // dead
        var energyAfterDeath = creature.Energy;

        // Act
        creature.SpendOrLoseEnergy(3);

        // Assert
        creature.Energy.Should().Be(energyAfterDeath);
    }

    [Fact]
    public void GainEnergy_WhenAlive_ShouldIncreaseEnergy()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 10, energy: 2);

        // Act
        creature.GainEnergy(4);

        // Assert
        creature.Energy.Value.Should().Be(6);
    }

    [Fact]
    public void GainEnergy_WhenDead_ShouldDoNothing()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 5, energy: 2);
        creature.TakeDamage(10); // dead
        var energyAfterDeath = creature.Energy;

        // Act
        creature.GainEnergy(4);

        // Assert
        creature.Energy.Should().Be(energyAfterDeath);
    }

    [Fact]
    public void GainInitiative_WhenAlive_ShouldIncreaseInitiative()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 10, initiative: 5);

        // Act
        creature.GainInitiative(3);

        // Assert
        creature.CurrentInitiative.Value.Should().Be(8);
    }

    [Fact]
    public void SpendOrLoseInitiative_WhenAlive_ShouldReduceInitiative()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 10, initiative: 8);

        // Act
        creature.SpendOrLoseInitiative(3);

        // Assert
        creature.CurrentInitiative.Value.Should().Be(5);
    }

    [Fact]
    public void GainInitiative_WhenDead_ShouldDoNothing()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 5, initiative: 5);
        creature.TakeDamage(10); // dead
        var initiativeAfterDeath = creature.CurrentInitiative;

        // Act
        creature.GainInitiative(3);

        // Assert
        creature.CurrentInitiative.Should().Be(initiativeAfterDeath);
    }

    [Fact]
    public void SpendOrLoseInitiative_WhenDead_ShouldDoNothing()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 5, initiative: 5);
        creature.TakeDamage(10); // dead
        var initiativeAfterDeath = creature.CurrentInitiative;

        // Act
        creature.SpendOrLoseInitiative(3);

        // Assert
        creature.CurrentInitiative.Should().Be(initiativeAfterDeath);
    }

    [Fact]
    public void Methods_ShouldThrowArgumentException_OnNegativeValues()
    {
        // Arrange
        var creature = CreateBasicCreature(health: 10, energy: 5, initiative: 5);

        // Act
        var takeDamage = () => creature.TakeDamage(-1);
        var heal = () => creature.Heal(-1);
        var spendEnergy = () => creature.SpendOrLoseEnergy(-1);
        var gainEnergy = () => creature.GainEnergy(-1);
        var gainInitiative = () => creature.GainInitiative(-1);
        var spendInitiative = () => creature.SpendOrLoseInitiative(-1);

        // Assert
        takeDamage.Should().Throw<ArgumentException>();
        heal.Should().Throw<ArgumentException>();
        spendEnergy.Should().Throw<ArgumentException>();
        gainEnergy.Should().Throw<ArgumentException>();
        gainInitiative.Should().Throw<ArgumentException>();
        spendInitiative.Should().Throw<ArgumentException>();
    }

    // ---------- Helpers ----------

    private static CombatCreature CreateBasicCreature(
        int health = 10,
        int energy = 5,
        int initiative = 5)
    {
        var id = new CreatureId(1);

        var baseHp = Health.Of(health);
        var baseEnergy = Energy.Of(energy);
        var baseDefense = Defense.Of(0);
        var baseInitiative = Initiative.Of(initiative);
        var baseCrit = CriticalChance.Of(0);

        var template = CreateTemplate(
            baseHp,
            baseEnergy,
            baseDefense,
            baseInitiative,
            baseCrit,
            Array.Empty<SpellId>()
        );

        return CombatCreature.FromCreatureTemplate(template, id);
    }

    private static CreatureDefinitionRef CreateTemplate(
        Health baseHp,
        Energy baseEnergy,
        Defense baseDefense,
        Initiative baseInitiative,
        CriticalChance baseCrit,
        IReadOnlyList<SpellId> startingSpells)
    {

         return new CreatureDefinitionRef(
             Id: new CreatureDefId("creature:main:v1"),
             Name: "Test Creature",
             baseHp,
             baseEnergy,
             baseDefense,
             baseInitiative,
             baseCrit,
             startingSpells
         );
    }
}
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Infrastructure.Bootstrap;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Contracts.Resources.Creatures;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Tests.Services.Combat.Execution;

public class DamageComputationServiceTests
{
    private readonly DamageComputationService _sut = new();
    private readonly IGameResources _resources;

    public DamageComputationServiceTests()
    {
        var baseDir = AppContext.BaseDirectory;
        var schemaPath = Path.Combine(baseDir, "Data/dst", "game.schema.json");

        _resources = GameResourcesFactory.LoadFromFile(schemaPath.ToString());
    }

    [Fact]
    public void ComputeFinalDamage_WhenDefenderIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        const int rawDamage = 10;
        CreatureSnapshot defender = null!;

        // Act
        var act = () => _sut.ComputeFinalDamage(rawDamage, defender);

        // Assert
        act.Should()
            .Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("defender");
    }

    [Fact]
    public void ComputeFinalDamage_WhenDefenderIsDead_ReturnsZero()
    {
        // Arrange
        const int rawDamage = 10;
        var defender = CreateDeadSnapshot();

        // Act
        var result = _sut.ComputeFinalDamage(rawDamage, defender);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ComputeFinalDamage_WhenRawDamageIsZero_ReturnsZero()
    {
        // Arrange
        const int rawDamage = 0;
        var defender = CreateAliveSnapshot();

        // Act
        var result = _sut.ComputeFinalDamage(rawDamage, defender);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ComputeFinalDamage_WhenRawDamageIsNegative_ReturnsZero()
    {
        // Arrange
        const int rawDamage = -5;
        var defender = CreateAliveSnapshot();

        // Act
        var result = _sut.ComputeFinalDamage(rawDamage, defender);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ComputeFinalDamage_WhenDamageGreaterThanTotalDefense_ReturnsDamageMinusTotalDefense()
    {
        // Arrange
        var defender = CreateAliveSnapshot();
        var totalDefense = defender.TotalDefense.Value;
        var rawDamage = totalDefense + 5;

        // Act
        var result = _sut.ComputeFinalDamage(rawDamage, defender);

        // Assert
        result.Should().Be(rawDamage - totalDefense);
    }

    [Fact]
    public void ComputeFinalDamage_WhenDamageEqualToTotalDefense_ReturnsZero()
    {
        // Arrange
        var defender = CreateAliveSnapshot();
        var totalDefense = defender.TotalDefense.Value;
        var rawDamage = totalDefense;

        // Act
        var result = _sut.ComputeFinalDamage(rawDamage, defender);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ComputeFinalDamage_WhenDamageLowerThanTotalDefense_ReturnsZero()
    {
        // Arrange
        var defender = CreateAliveSnapshot();
        var totalDefense = defender.TotalDefense.Value;
        var rawDamage = Math.Max(0, totalDefense - 1);

        // Act
        var result = _sut.ComputeFinalDamage(rawDamage, defender);

        // Assert
        result.Should().Be(0);
    }

    // ---------- Helpers ----------

    private CreatureSnapshot CreateAliveSnapshot()
    {
        var id = new CreatureId(1);
        var def = _resources.GetCreature(new CreatureDefId("creature:main:v1"));
        var playerSlot = PlayerSlot.Player2;
        var creature = CombatCreature.FromCreatureTemplate(def, id, playerSlot);

        return CreatureSnapshot.From(creature);
    }

    private CreatureSnapshot CreateDeadSnapshot()
    {
        var id = new CreatureId(1);
        var def = _resources.GetCreature(new CreatureDefId("creature:main:v1"));
        var playerSlot = PlayerSlot.Player2;
        var creature = CombatCreature.FromCreatureTemplate(def, id, playerSlot);

        // Kill the creature so the snapshot reflects a dead state
        creature.TakeDamage(creature.Health.Value + 50);

        return CreatureSnapshot.From(creature);
    }
}

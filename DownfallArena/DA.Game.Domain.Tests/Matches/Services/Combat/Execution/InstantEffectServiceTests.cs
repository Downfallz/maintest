using AutoFixture.Xunit2;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Infrastructure.Bootstrap;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Tests;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Tests.Services.Combat.Execution;

public class InstantEffectServiceTests
{
    private readonly IGameResources _resources;

    public InstantEffectServiceTests()
    {
        var baseDir = AppContext.BaseDirectory;
        var schemaPath = Path.Combine(baseDir, "Data/dst", "game.schema.json");

        _resources = GameResourcesFactory.LoadFromFile(schemaPath.ToString());
    }

    [Theory]
    [GameAutoData]
    public void ApplyInstantEffect_WhenTargetIsNull_ThrowsArgumentNullException(
        InstantEffectApplication effect,
        InstantEffectService sut)
    {
        // Arrange
        CombatCreature target = null!;

        // Act
        var act = () => sut.ApplyInstantEffect(effect, target);

        // Assert
        act.Should()
            .Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("target");
    }

    [Theory]
    [GameAutoData]
    public void ApplyInstantEffect_WhenEffectIsNull_ThrowsArgumentNullException(
        CombatCreature target,
        InstantEffectService sut)
    {
        // Arrange
        InstantEffectApplication effect = null!;

        // Act
        var act = () => sut.ApplyInstantEffect(effect, target);

        // Assert
        act.Should()
            .Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("eff");
    }

    [Theory]
    [GameAutoData]
    public void ApplyInstantEffect_WithNonDamageEffect_ShouldNotCallDamageService_AndNotChangeHealth(
        [Frozen] Mock<IDamageComputationService> damageService,
        InstantEffectService sut)
    {
        // Arrange
        var target = CreateBasicCreature();
        var initialHealth = target.Health;
        var effect = CreateInstantEffect(
            kind: EffectKind.Heal,  // any non-damage kind
            amount: 10
        );

        // Act
        sut.ApplyInstantEffect(effect, target);

        // Assert
        damageService.Verify(
            x => x.ComputeFinalDamage(It.IsAny<int>(), It.IsAny<CreatureSnapshot>()),
            Times.Never);

        target.Health.Should().Be(initialHealth);
    }

    [Theory]
    [InlineGameAutoData(999, 7)]
    public void ApplyInstantEffect_WithDamageEffect_ShouldUseComputedDamageNotRawAmount(
        int rawAmount,
        int computedDamage,
        [Frozen] Mock<IDamageComputationService> damageService,
        InstantEffectService sut)
    {
        // Arrange
        var target = CreateBasicCreature();
        var initialHealth = target.Health.Value;

        var effect = CreateInstantEffect(
            kind: EffectKind.Damage,
            amount: rawAmount
        );

        damageService
            .Setup(x => x.ComputeFinalDamage(rawAmount, It.IsAny<CreatureSnapshot>()))
            .Returns(computedDamage);

        // Act
        sut.ApplyInstantEffect(effect, target);

        // Assert
        damageService.Verify(
            x => x.ComputeFinalDamage(rawAmount, It.IsAny<CreatureSnapshot>()),
            Times.Once);

        target.Health.Value.Should().Be(initialHealth - Math.Max(0, computedDamage));
    }

    [Theory]
    [InlineGameAutoData(10, 0)]
    [InlineGameAutoData(10, -5)]
    public void ApplyInstantEffect_WhenComputedDamageIsZeroOrNegative_ShouldNotChangeHealth(
        int rawAmount,
        int computedDamage,
        [Frozen] Mock<IDamageComputationService> damageService,
        InstantEffectService sut)
    {
        // Arrange
        var target = CreateBasicCreature();
        var initialHealth = target.Health;

        var effect = CreateInstantEffect(
            kind: EffectKind.Damage,
            amount: rawAmount
        );

        damageService
            .Setup(x => x.ComputeFinalDamage(rawAmount, It.IsAny<CreatureSnapshot>()))
            .Returns(computedDamage);

        // Act
        sut.ApplyInstantEffect(effect, target);

        // Assert
        target.Health.Should().Be(initialHealth);

        damageService.Verify(
            x => x.ComputeFinalDamage(rawAmount, It.IsAny<CreatureSnapshot>()),
            Times.Once);
    }

    // ---------- Helpers ----------

    private CombatCreature CreateBasicCreature()
    {
        var id = new CreatureId(1);
        var def = _resources.GetCreature(new CreatureDefId("creature:main:v1"));
        var playerSlot = PlayerSlot.Player2;
        return CombatCreature.FromCreatureTemplate(def, id, playerSlot);
    }
    private static InstantEffectApplication CreateInstantEffect(
    EffectKind kind,
    int amount)
    {
        return new InstantEffectApplication(
            ActorId: new CreatureId(999),     // arbitrary
            TargetId: new CreatureId(1),      // always matches our CreateBasicCreature
            Kind: kind,
            Amount: amount
        );
    }
}

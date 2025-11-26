using AutoFixture.Xunit2;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
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
using System.Reflection.Metadata;
using System.Text;

using Match = DA.Game.Domain2.Matches.Aggregates.Match;
namespace DA.Game.Domain.Tests.Matches.Services.Combat.Execution;

public class EffectExecutionServiceTests
{
    private readonly IGameResources _resources;

    public EffectExecutionServiceTests()
    {
        var baseDir = AppContext.BaseDirectory;
        var schemaPath = Path.Combine(baseDir, "Data/dst", "game.schema.json");

        _resources = GameResourcesFactory.LoadFromFile(schemaPath.ToString());
    }

    private CombatCreature CreateCreature(CreatureId id)
    {
        var def = _resources.GetCreature(new CreatureDefId("creature:main:v1"));
        var playerSlot = PlayerSlot.Player2;
        return CombatCreature.FromCreatureTemplate(def, id, playerSlot);
    }

    private static CombatActionResult CreateActionResult(params InstantEffectApplication[] instantEffects)
    {
        return new CombatActionResult(
            choice: null!,                 // irrelevant for this test
            instantEffects: instantEffects,
            critical: CritComputationResult.Normal(0, 0)
        );
    }

    // ------------------------------------------------------------
    // TESTS
    // ------------------------------------------------------------

    [Theory]
    [GameAutoData]
    public void ApplyCombatResult_WhenResultIsNull_ThrowsArgumentNullException(
        IReadOnlyList<CombatCreature> creatures,
        EffectExecutionService sut)
    {
        CombatActionResult result = null!;

        var act = () => sut.ApplyCombatResult(result, creatures);

        act.Should()
            .Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("result");
    }

    [Theory]
    [GameAutoData]
    public void ApplyCombatResult_WhenCreaturesNull_ThrowsArgumentNullException(
        CombatActionResult result,
        EffectExecutionService sut)
    {
        IReadOnlyList<CombatCreature> allCreatures = null!;

        var act = () => sut.ApplyCombatResult(result, allCreatures);

        act.Should()
            .Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("allCreatures");
    }

    [Theory]
    [GameAutoData]
    public void ApplyCombatResult_WhenNoInstantEffects_ReturnsOk_AndDoesNotCallInstantService(
        [Frozen] Mock<IInstantEffectService> instantSvc,
        EffectExecutionService sut)
    {
        var creatures = new List<CombatCreature>();

        var result = CreateActionResult(); // empty list

        var res = sut.ApplyCombatResult(result, creatures);

        res.IsSuccess.Should().BeTrue();

        instantSvc.Verify(
            x => x.ApplyInstantEffect(It.IsAny<InstantEffectApplication>(), It.IsAny<CombatCreature>()),
            Times.Never);
    }

    [Theory]
    [GameAutoData]
    public void ApplyCombatResult_WhenTargetNotFound_ReturnsFailure_AndDoesNotCallInstantService(
        [Frozen] Mock<IInstantEffectService> instantSvc,
        EffectExecutionService sut)
    {
        var creatures = new List<CombatCreature>(); // empty → target not found

        var effect = new InstantEffectApplication(
            ActorId: new CreatureId(100),
            TargetId: new CreatureId(999),
            Kind: EffectKind.Damage,
            Amount: 10
        );

        var result = CreateActionResult(effect);

        var res = sut.ApplyCombatResult(result, creatures);

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Be("Could not find creature in match");

        instantSvc.Verify(
            x => x.ApplyInstantEffect(It.IsAny<InstantEffectApplication>(), It.IsAny<CombatCreature>()),
            Times.Never);
    }

    [Theory]
    [GameAutoData]
    public void ApplyCombatResult_WhenValidTarget_CallsInstantServiceForEachEffect(
        [Frozen] Mock<IInstantEffectService> instantSvc,
        EffectExecutionService sut)
    {
        var id = new CreatureId(1);
        var creature = CreateCreature(id);
        var creatures = new List<CombatCreature> { creature };

        var e1 = new InstantEffectApplication(new CreatureId(10), id, EffectKind.Damage, 5);
        var e2 = new InstantEffectApplication(new CreatureId(11), id, EffectKind.Damage, 7);

        var result = CreateActionResult(e1, e2);

        var res = sut.ApplyCombatResult(result, creatures);

        res.IsSuccess.Should().BeTrue();

        instantSvc.Verify(x => x.ApplyInstantEffect(e1, creature), Times.Once);
        instantSvc.Verify(x => x.ApplyInstantEffect(e2, creature), Times.Once);
    }

    [Theory]
    [GameAutoData]
    public void ApplyCombatResult_WhenSecondEffectHasMissingTarget_StopsProcessing(
        [Frozen] Mock<IInstantEffectService> instantSvc,
        EffectExecutionService sut)
    {
        var validId = new CreatureId(1);
        var creature = CreateCreature(validId);

        var creatures = new List<CombatCreature> { creature };

        var first = new InstantEffectApplication(new CreatureId(10), validId, EffectKind.Damage, 5);

        var missing = new InstantEffectApplication(new CreatureId(11), new CreatureId(999), EffectKind.Damage, 10);

        var result = CreateActionResult(first, missing);

        var res = sut.ApplyCombatResult(result, creatures);

        res.IsSuccess.Should().BeFalse();

        // First effect applied successfully
        instantSvc.Verify(x => x.ApplyInstantEffect(first, creature), Times.Once);

        // Second effect should NOT be applied
        instantSvc.Verify(x => x.ApplyInstantEffect(missing, It.IsAny<CombatCreature>()), Times.Never);
    }
}

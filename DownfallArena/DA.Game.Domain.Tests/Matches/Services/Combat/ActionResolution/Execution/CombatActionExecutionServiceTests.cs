using AutoFixture.Xunit2;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Infrastructure.Bootstrap;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Contracts.Resources.Stats;
using DA.Game.Shared.Tests;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using Match = DA.Game.Domain2.Matches.Aggregates.Match;

namespace DA.Game.Domain.Tests.Matches.Services.Combat.ActionResolution.Execution
{
    public class CombatActionExecutionServiceTests
    {
        private readonly IGameResources _resources;

        public CombatActionExecutionServiceTests()
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

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------

        private static Spell CreateFakeSpell()
        {
            var effects = new IEffect[]
            {
                Damage.Of(1)
            };

            return Spell.Create(
                id: new SpellId("spell:test:execution"),
                name: "Test Execution Spell",
                spellType: SpellType.Offensive,
                classType: CreatureClass.Creature,
                initiative: Initiative.Of(0),
                energyCost: Energy.Of(0),
                critChance: CriticalChance.Of(0),
                targetingSpec: TargetingSpec.Of(TargetOrigin.Any, TargetScope.Multi),
                effects: effects);
        }

        private static CombatActionResult CreateActionResult(params InstantEffectApplication[] instantEffects)
        {
            var spell = CreateFakeSpell();

            var choice = new CombatActionChoice(
                ActorId: new CreatureId(1),
                SpellRef: spell,
                TargetIds: Array.Empty<CreatureId>());

            return new CombatActionResult(
                originalChoice: choice,
                effectiveChoice: choice,
                instantEffects: instantEffects,
                critical: CritComputationResult.Normal(0, 0),
                targetingFailures: Array.Empty<TargetingFailure>());
        }

        // ------------------------------------------------------------
        // TESTS
        // ------------------------------------------------------------

        [Theory]
        [GameAutoData]
        public void ApplyCombatResult_WhenResultIsNull_ThrowsArgumentNullException(
            IReadOnlyList<CombatCreature> creatures,
            CombatActionExecutionService sut)
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
            CombatActionExecutionService sut)
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
            CombatActionExecutionService sut)
        {
            var creatures = new List<CombatCreature>();

            var result = CreateActionResult(); // empty list

            var res = sut.ApplyCombatResult(result, creatures);

            res.IsSuccess.Should().BeTrue();
            res.IsInvariant.Should().BeFalse();

            instantSvc.Verify(
                x => x.ApplyInstantEffect(
                    It.IsAny<InstantEffectApplication>(),
                    It.IsAny<CombatCreature>()),
                Times.Never);
        }

        [Theory]
        [GameAutoData]
        public void ApplyCombatResult_WhenTargetNotFound_ReturnsInvariantFailure_AndDoesNotCallInstantService(
            [Frozen] Mock<IInstantEffectService> instantSvc,
            CombatActionExecutionService sut)
        {
            var creatures = new List<CombatCreature>(); // empty => target not found

            var effect = new InstantEffectApplication(
                ActorId: new CreatureId(100),
                TargetId: new CreatureId(999),
                Kind: EffectKind.Damage,
                Amount: 10
            );

            var result = CreateActionResult(effect);

            var res = sut.ApplyCombatResult(result, creatures);

            res.IsSuccess.Should().BeFalse();
            res.IsInvariant.Should().BeTrue();
            res.Error.Should().Be("I201 - Target creature was not found in match when applying combat result.");

            instantSvc.Verify(
                x => x.ApplyInstantEffect(It.IsAny<InstantEffectApplication>(), It.IsAny<CombatCreature>()),
                Times.Never);
        }

        [Theory]
        [GameAutoData]
        public void ApplyCombatResult_WhenValidTarget_CallsInstantServiceForEachEffect(
            [Frozen] Mock<IInstantEffectService> instantSvc,
            CombatActionExecutionService sut)
        {
            var id = new CreatureId(1);
            var creature = CreateCreature(id);
            var creatures = new List<CombatCreature> { creature };

            var e1 = new InstantEffectApplication(new CreatureId(10), id, EffectKind.Damage, 5);
            var e2 = new InstantEffectApplication(new CreatureId(11), id, EffectKind.Damage, 7);

            var result = CreateActionResult(e1, e2);

            var res = sut.ApplyCombatResult(result, creatures);

            res.IsSuccess.Should().BeTrue();
            res.IsInvariant.Should().BeFalse();

            instantSvc.Verify(x => x.ApplyInstantEffect(e1, creature), Times.Once);
            instantSvc.Verify(x => x.ApplyInstantEffect(e2, creature), Times.Once);
        }

        [Theory]
        [GameAutoData]
        public void ApplyCombatResult_WhenSecondEffectHasMissingTarget_StopsProcessing(
            [Frozen] Mock<IInstantEffectService> instantSvc,
            CombatActionExecutionService sut)
        {
            var validId = new CreatureId(1);
            var creature = CreateCreature(validId);

            var creatures = new List<CombatCreature> { creature };

            var first = new InstantEffectApplication(new CreatureId(10), validId, EffectKind.Damage, 5);
            var missing = new InstantEffectApplication(new CreatureId(11), new CreatureId(999), EffectKind.Damage, 10);

            var result = CreateActionResult(first, missing);

            var res = sut.ApplyCombatResult(result, creatures);

            res.IsSuccess.Should().BeFalse();
            res.IsInvariant.Should().BeTrue();

            // First effect applied successfully
            instantSvc.Verify(x => x.ApplyInstantEffect(first, creature), Times.Once);

            // Second effect should NOT be applied
            instantSvc.Verify(x => x.ApplyInstantEffect(missing, It.IsAny<CombatCreature>()), Times.Never);
        }
    }
}

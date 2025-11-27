using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Enums;
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

public sealed class TargetingPolicyV1Tests
{
    // --------------------------
    // Guard clauses
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCombatActionHasValidTargets_WhenContextIsNull_ThrowsArgumentNullException(
        CombatActionChoice choice,
        TargetingPolicyV1 sut)
    {
        CreaturePerspective ctx = null!;

        var act = () => sut.EnsureCombatActionHasValidTargets(ctx, choice);

        act.Should()
            .Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("ctx");
    }

    [Theory, MatchAutoData]
    public void EnsureCombatActionHasValidTargets_WhenChoiceIsNull_ThrowsArgumentNullException(
        CreaturePerspective ctx,
        TargetingPolicyV1 sut)
    {
        CombatActionChoice choice = null!;

        var act = () => sut.EnsureCombatActionHasValidTargets(ctx, choice);

        act.Should()
            .Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("choice");
    }

    // --------------------------
    // Success case
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCombatActionHasValidTargets_WhenTargetsValid_Succeeds(
        CreaturePerspective baseCtx,
        TargetingPolicyV1 sut)
    {
        // Self + SingleTarget -> actor targets itself
        var spell = CreateSpell(
            origin: TargetOrigin.Self,
            scope: TargetScope.SingleTarget,
            maxTargets: 1);

        var choice = new CombatActionChoice(
            ActorId: baseCtx.ActorId,
            SpellRef: spell,
            TargetIds: new[] { baseCtx.ActorId });

        var result = sut.EnsureCombatActionHasValidTargets(baseCtx, choice);

        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();

        var report = result.Value;
        report.Should().NotBeNull();
        report.HasErrors.Should().BeFalse();
        report.Failures.Should().BeEmpty();
    }

    // --------------------------
    // D401 - Target not found
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCombatActionHasValidTargets_WhenTargetNotFound_ReportContainsD401(
        CreaturePerspective baseCtx,
        TargetingPolicyV1 sut)
    {
        // Arrange: create an id that does not exist in the match
        var unknownId = new CreatureId(9999);
        while (baseCtx.Creatures.Any(c => c.CharacterId == unknownId))
        {
            unknownId = new CreatureId(unknownId.Value + 1);
        }

        var spell = CreateSpell(
            origin: TargetOrigin.Any,
            scope: TargetScope.Multi,
            maxTargets: null); // no cap

        var choice = new CombatActionChoice(
            ActorId: baseCtx.ActorId,
            SpellRef: spell,
            TargetIds: new[] { unknownId });

        // Act
        var result = sut.EnsureCombatActionHasValidTargets(baseCtx, choice);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();

        var report = result.Value;
        report.Should().NotBeNull();
        report.HasErrors.Should().BeTrue();

        report.Failures.Should()
            .Contain(f =>
                f.ErrorCode == "D401" &&
                f.TargetId == unknownId &&
                f.Message == "D401 - Target not found in match.");
    }

    // --------------------------
    // D403 - Too many targets
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCombatActionHasValidTargets_WhenTooManyTargets_ReportContainsD403(
        CreaturePerspective baseCtx,
        TargetingPolicyV1 sut)
    {
        var spell = CreateSpell(
            origin: TargetOrigin.Any,
            scope: TargetScope.Multi,
            maxTargets: 1);

        // Two existing, alive targets
        var t1 = baseCtx.ActorId;
        var t2 = baseCtx.Creatures.First(c => c.CharacterId != baseCtx.ActorId).CharacterId;

        var choice = new CombatActionChoice(
            ActorId: baseCtx.ActorId,
            SpellRef: spell,
            TargetIds: new[] { t1, t2 });

        var result = sut.EnsureCombatActionHasValidTargets(baseCtx, choice);

        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();

        var report = result.Value;
        report.HasErrors.Should().BeTrue();
        report.Failures.Should().HaveCount(1);

        var failure = report.Failures.Single();
        failure.TargetId.Should().BeNull();
        failure.ErrorCode.Should().Be("D403");
        failure.Message.Should().Be("D403 - Too many targets provided for this spell.");
    }

    // --------------------------
    // D404 - Self target required
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCombatActionHasValidTargets_WhenSelfOriginButTargetsOther_ReportContainsD404(
        CreaturePerspective baseCtx,
        TargetingPolicyV1 sut)
    {
        var other = baseCtx.Creatures
            .First(c => c.CharacterId != baseCtx.ActorId);

        var spell = CreateSpell(
            origin: TargetOrigin.Self,
            scope: TargetScope.Multi,
            maxTargets: null);

        var choice = new CombatActionChoice(
            ActorId: baseCtx.ActorId,
            SpellRef: spell,
            TargetIds: new[] { other.CharacterId });

        var result = sut.EnsureCombatActionHasValidTargets(baseCtx, choice);

        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();

        var report = result.Value;
        report.HasErrors.Should().BeTrue();
        report.Failures.Should().HaveCount(1);

        var failure = report.Failures.Single();
        failure.TargetId.Should().BeNull();
        failure.ErrorCode.Should().Be("D404");
        failure.Message.Should().Be("D404 - Self-target spell must target the acting creature only.");
    }

    // --------------------------
    // D405 - Allies only (origin Ally, includes enemy)
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCombatActionHasValidTargets_WhenOriginAllyButIncludesEnemy_ReportContainsD405(
        CreaturePerspective baseCtx,
        TargetingPolicyV1 sut)
    {
        var actorOwner = baseCtx.Actor.OwnerSlot;

        // Find an ally different from the actor
        var ally = baseCtx.Creatures
            .First(c => c.OwnerSlot == actorOwner && c.CharacterId != baseCtx.ActorId);

        // Try to find an existing enemy
        var existingEnemy = baseCtx.Creatures
            .FirstOrDefault(c => c.OwnerSlot != actorOwner);

        CreaturePerspective ctx;
        CreatureId enemyId;

        if (existingEnemy is null)
        {
            // No enemy present: create one
            var template = baseCtx.Creatures
                .First(c => c.CharacterId != baseCtx.ActorId);

            var otherSlot = actorOwner == PlayerSlot.Player1
                ? PlayerSlot.Player2
                : PlayerSlot.Player1;

            var enemySnapshot = CloneUtility.CloneSnapshot(
                template,
                ownerSlot: otherSlot);

            var newCreatures = baseCtx.Creatures
                .Where(c => c.CharacterId != enemySnapshot.CharacterId)
                .Append(enemySnapshot)
                .ToList();

            ctx = baseCtx with { Creatures = newCreatures };
            enemyId = enemySnapshot.CharacterId;
        }
        else
        {
            ctx = baseCtx;
            enemyId = existingEnemy.CharacterId;
        }

        var spell = CreateSpell(
            origin: TargetOrigin.Ally,
            scope: TargetScope.Multi,
            maxTargets: null);

        var choice = new CombatActionChoice(
            ActorId: ctx.ActorId,
            SpellRef: spell,
            TargetIds: new[] { ally.CharacterId, enemyId });

        // Act
        var result = sut.EnsureCombatActionHasValidTargets(ctx, choice);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();

        var report = result.Value;
        report.HasErrors.Should().BeTrue();

        report.Failures.Should()
            .Contain(f =>
                f.ErrorCode == "D405" &&
                f.TargetId == enemyId &&
                f.Message == "D405 - All targets must be allies.");
    }

    // --------------------------
    // D406 - Enemies only (origin Enemy, includes ally)
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCombatActionHasValidTargets_WhenOriginEnemyButIncludesAlly_ReportContainsD406(
        CreaturePerspective baseCtx,
        TargetingPolicyV1 sut)
    {
        var actorOwner = baseCtx.Actor.OwnerSlot;
        var ally = baseCtx.Creatures
            .First(c => c.OwnerSlot == actorOwner && c.CharacterId != baseCtx.ActorId);

        var spell = CreateSpell(
            origin: TargetOrigin.Enemy,
            scope: TargetScope.Multi,
            maxTargets: null);

        var choice = new CombatActionChoice(
            ActorId: baseCtx.ActorId,
            SpellRef: spell,
            TargetIds: new[] { ally.CharacterId });

        var result = sut.EnsureCombatActionHasValidTargets(baseCtx, choice);

        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();

        var report = result.Value;
        report.HasErrors.Should().BeTrue();

        report.Failures.Should()
            .ContainSingle(f =>
                f.ErrorCode == "D406" &&
                f.TargetId == ally.CharacterId &&
                f.Message == "D406 - All targets must be enemies.");
    }

    // --------------------------
    // D407 - SingleTarget scope with multiple targets
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCombatActionHasValidTargets_WhenScopeSingleTargetWithTwoTargets_ReportContainsD407(
        CreaturePerspective baseCtx,
        TargetingPolicyV1 sut)
    {
        var spell = CreateSpell(
            origin: TargetOrigin.Any,
            scope: TargetScope.SingleTarget,
            maxTargets: null); // no extra cap here

        var t1 = baseCtx.ActorId;
        var t2 = baseCtx.Creatures.First(c => c.CharacterId != baseCtx.ActorId).CharacterId;

        var choice = new CombatActionChoice(
            ActorId: baseCtx.ActorId,
            SpellRef: spell,
            TargetIds: new[] { t1, t2 });

        var result = sut.EnsureCombatActionHasValidTargets(baseCtx, choice);

        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();

        var report = result.Value;
        report.HasErrors.Should().BeTrue();
        report.Failures.Should().HaveCount(1);

        var failure = report.Failures.Single();
        failure.TargetId.Should().BeNull();
        failure.ErrorCode.Should().Be("D407");
        failure.Message.Should().Be("D407 - This spell must target exactly one creature.");
    }

    // --------------------------
    // D408 - Multi scope with no targets (coexists with D402)
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCombatActionHasValidTargets_WhenScopeMultiWithNoTargets_ReportContainsD408(
        CreaturePerspective baseCtx,
        TargetingPolicyV1 sut)
    {
        var spell = CreateSpell(
            origin: TargetOrigin.Any,
            scope: TargetScope.Multi,
            maxTargets: 3);

        var choice = new CombatActionChoice(
            ActorId: baseCtx.ActorId,
            SpellRef: spell,
            TargetIds: Array.Empty<CreatureId>());

        var result = sut.EnsureCombatActionHasValidTargets(baseCtx, choice);

        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();

        var report = result.Value;
        report.HasErrors.Should().BeTrue();

        // We only assert the presence of D408; D402 may also be present.
        report.Failures.Should()
            .Contain(f => f.ErrorCode == "D408" &&
                          f.Message == "D408 - This spell must target at least one creature.");
    }

    // --------------------------
    // D409 - Target dead
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureCombatActionHasValidTargets_WhenSingleTargetDead_ReportContainsD409(
        CreaturePerspective baseCtx,
        TargetingPolicyV1 sut)
    {
        var deadCandidate = baseCtx.Creatures
            .First(c => c.CharacterId != baseCtx.ActorId);

        var deadSnapshot = CloneUtility.CloneSnapshot(
            deadCandidate,
            health: Health.Of(0));

        var updatedCreatures = baseCtx.Creatures
            .Select(c => c.CharacterId == deadSnapshot.CharacterId ? deadSnapshot : c)
            .ToList();

        var ctx = baseCtx with { Creatures = updatedCreatures };

        var spell = CreateSpell(
            origin: TargetOrigin.Any,
            scope: TargetScope.Multi,
            maxTargets: null);

        var choice = new CombatActionChoice(
            ActorId: ctx.ActorId,
            SpellRef: spell,
            TargetIds: new[] { deadSnapshot.CharacterId });

        var result = sut.EnsureCombatActionHasValidTargets(ctx, choice);

        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();

        var report = result.Value;
        report.HasErrors.Should().BeTrue();
        report.Failures.Should().HaveCount(1);

        var failure = report.Failures.Single();
        failure.TargetId.Should().Be(deadSnapshot.CharacterId);
        failure.ErrorCode.Should().Be("D409");
        failure.Message.Should().Be("D409 - Target creature is dead and cannot be targeted.");
    }

    // --------------------------
    // Helpers
    // --------------------------

    private static Spell CreateSpell(
        TargetOrigin origin,
        TargetScope scope,
        int? maxTargets = null)
    {
        var effects = new IEffect[]
        {
            Damage.Of(1)
        };

        return Spell.Create(
            id: new SpellId("spell:test:target:v1"),
            name: "Test Target Spell",
            spellType: SpellType.Offensive,
            classType: CreatureClass.Creature,
            initiative: Initiative.Of(0),
            energyCost: Energy.Of(0),
            critChance: CriticalChance.Of(0),
            targetingSpec: TargetingSpec.Of(origin, scope, maxTargets),
            effects: effects);
    }
}

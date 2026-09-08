using AutoFixture;
using AutoFixture.Xunit2;
using DA.Game.Domain.Tests; // MatchAutoDataAttribute
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.Services.Combat;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;
using FluentAssertions;
using Moq;
using System.Linq;
using Xunit;

namespace DA.Game.Domain2.Tests.Matches.Services.Combat;

public sealed class CombatActionResolutionServiceTests
{
    // 1) If action policy fails, we should return the same failure and stop.
    [Theory]
    [MatchAutoData]
    public void Resolve_WhenActionPolicyFails_ShouldReturnFailureAndSkipOthers(
        [Frozen] Mock<ICombatActionResolutionPolicy> actionPolicyMock,
        [Frozen] Mock<ICostPolicy> costPolicyMock,
        [Frozen] Mock<ITargetingPolicy> targetingPolicyMock,
        [Frozen] Mock<IEffectComputationService> effectServiceMock,
        [Frozen] Mock<ICritComputationService> critServiceMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        CombatActionResolutionService sut)
    {
        const string errorCode = "D401_ACTION_NOT_ALLOWED";

        actionPolicyMock
            .Setup(x => x.EnsureActionIsValid(ctx))
            .Returns(Result.Fail(errorCode));

        var result = sut.Resolve(ctx, choice);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(errorCode);
        result.IsInvariant.Should().BeFalse();

        costPolicyMock.Verify(x => x.EnsureCreatureHasEnoughEnergy(It.IsAny<CreaturePerspective>(), It.IsAny<Spell>()), Times.Never);
        targetingPolicyMock.Verify(x => x.EnsureCombatActionHasValidTargets(It.IsAny<CreaturePerspective>(), It.IsAny<CombatActionChoice>()), Times.Never);
        effectServiceMock.Verify(x => x.ComputeRawEffects(It.IsAny<CombatActionChoice>()), Times.Never);
        critServiceMock.Verify(x => x.ApplyCrit(It.IsAny<CreaturePerspective>(), It.IsAny<CombatActionChoice>()), Times.Never);
    }

    // 2) If cost policy fails, we should return failure and skip targeting/effects/crit.
    [Theory]
    [MatchAutoData]
    public void Resolve_WhenCostPolicyFails_ShouldReturnFailureAndSkipLaterSteps(
        [Frozen] Mock<ICombatActionResolutionPolicy> actionPolicyMock,
        [Frozen] Mock<ICostPolicy> costPolicyMock,
        [Frozen] Mock<ITargetingPolicy> targetingPolicyMock,
        [Frozen] Mock<IEffectComputationService> effectServiceMock,
        [Frozen] Mock<ICritComputationService> critServiceMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        CombatActionResolutionService sut)
    {
        actionPolicyMock
            .Setup(x => x.EnsureActionIsValid(ctx))
            .Returns(Result.Ok());

        const string errorCode = "D402_NOT_ENOUGH_ENERGY";

        costPolicyMock
            .Setup(x => x.EnsureCreatureHasEnoughEnergy(ctx, choice.SpellRef))
            .Returns(Result.Fail(errorCode));

        var result = sut.Resolve(ctx, choice);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(errorCode);
        result.IsInvariant.Should().BeFalse();

        targetingPolicyMock.Verify(x => x.EnsureCombatActionHasValidTargets(It.IsAny<CreaturePerspective>(), It.IsAny<CombatActionChoice>()), Times.Never);
        effectServiceMock.Verify(x => x.ComputeRawEffects(It.IsAny<CombatActionChoice>()), Times.Never);
        critServiceMock.Verify(x => x.ApplyCrit(It.IsAny<CreaturePerspective>(), It.IsAny<CombatActionChoice>()), Times.Never);
    }

    // 3) Targeting returns per-target failures → partial fizzle, some targets ignored.
    [Theory]
    [MatchAutoData]
    public void Resolve_WhenTargetingHasPerTargetFailures_ShouldFilterTargetsAndReturnPartialResult(
        [Frozen] Mock<ICombatActionResolutionPolicy> actionPolicyMock,
        [Frozen] Mock<ICostPolicy> costPolicyMock,
        [Frozen] Mock<ITargetingPolicy> targetingPolicyMock,
        [Frozen] Mock<IEffectComputationService> effectServiceMock,
        [Frozen] Mock<ICritComputationService> critServiceMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        InstantEffectApplication instantEffectSample,
        CritComputationResult critResult,
        CombatActionResolutionService sut,
        IFixture fixture)
    {
        actionPolicyMock
            .Setup(x => x.EnsureActionIsValid(ctx))
            .Returns(Result.Ok());

        // Use It.IsAny because we mutate choice later (record instance changes).
        costPolicyMock
            .Setup(x => x.EnsureCreatureHasEnoughEnergy(ctx, It.IsAny<Spell>()))
            .Returns(Result.Ok());

        // Two explicit targets: t1 is valid, t2 is invalid.
        var t1 = fixture.Create<CreatureId>();
        var t2 = fixture.Create<CreatureId>();

        choice = choice with
        {
            TargetIds = new[] { t1, t2 }
        };

        var perTargetFailures = new[]
        {
            new TargetingFailure(
                TargetId: t2,
                ErrorCode: "D405",
                Message: "Target 2 is invalid.")
        };

        var report = new TargetingCheckReport(perTargetFailures);

        targetingPolicyMock
            .Setup(x => x.EnsureCombatActionHasValidTargets(ctx, choice))
            .Returns(Result<TargetingCheckReport>.Ok(report));

        effectServiceMock
            .Setup(x => x.ComputeRawEffects(It.Is<CombatActionChoice>(c =>
                c.TargetIds != null &&
                c.TargetIds.Count == 1 &&
                c.TargetIds[0] == t1)))
            .Returns(new RawEffectBundle(
                new[] { instantEffectSample },
                Array.Empty<ConditionApplication>()));

        critServiceMock
            .Setup(x => x.ApplyCrit(ctx, It.Is<CombatActionChoice>(c =>
                c.TargetIds != null &&
                c.TargetIds.Count == 1 &&
                c.TargetIds[0] == t1)))
            .Returns(critResult);

        var result = sut.Resolve(ctx, choice);

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();

        var value = result.Value!;
        value.OriginalChoice.Should().Be(choice);
        value.EffectiveChoice.TargetIds.Should().ContainSingle()
            .Which.Should().Be(t1);

        value.InstantEffects.Should().ContainSingle()
            .Which.Should().Be(instantEffectSample);

        value.Critical.Should().Be(critResult);

        value.TargetingFailures.Should().HaveCount(1);
        value.TargetingFailures[0].TargetId.Should().Be(t2);
        value.TargetingFailures[0].ErrorCode.Should().Be("D405");
    }

    // 4) All targets invalid → full fizzle, no effects / crit called.
    [Theory]
    [MatchAutoData]
    public void Resolve_WhenAllTargetsInvalid_ShouldReturnFailureAndSkipEffects(
        [Frozen] Mock<ICombatActionResolutionPolicy> actionPolicyMock,
        [Frozen] Mock<ICostPolicy> costPolicyMock,
        [Frozen] Mock<ITargetingPolicy> targetingPolicyMock,
        [Frozen] Mock<IEffectComputationService> effectServiceMock,
        [Frozen] Mock<ICritComputationService> critServiceMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        CombatActionResolutionService sut,
        IFixture fixture)
    {
        actionPolicyMock
            .Setup(x => x.EnsureActionIsValid(ctx))
            .Returns(Result.Ok());

        costPolicyMock
            .Setup(x => x.EnsureCreatureHasEnoughEnergy(ctx, It.IsAny<Spell>()))
            .Returns(Result.Ok());

        var t1 = fixture.Create<CreatureId>();
        var t2 = fixture.Create<CreatureId>();

        choice = choice with
        {
            TargetIds = new[] { t1, t2 }
        };

        var perTargetFailures = new[]
        {
            new TargetingFailure(t1, "D405", "T1 invalid"),
            new TargetingFailure(t2, "D405", "T2 invalid")
        };

        var report = new TargetingCheckReport(perTargetFailures);

        targetingPolicyMock
            .Setup(x => x.EnsureCombatActionHasValidTargets(ctx, choice))
            .Returns(Result<TargetingCheckReport>.Ok(report));

        var result = sut.Resolve(ctx, choice);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(perTargetFailures[0].Message);
        result.Value.Should().BeNull();

        effectServiceMock.Verify(x => x.ComputeRawEffects(It.IsAny<CombatActionChoice>()), Times.Never);
        critServiceMock.Verify(x => x.ApplyCrit(It.IsAny<CreaturePerspective>(), It.IsAny<CombatActionChoice>()), Times.Never);
    }
}

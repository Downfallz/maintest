using AutoFixture.Xunit2;
using DA.Game.Domain.Tests;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;
using FluentAssertions;
using Moq;
using Xunit;

namespace DA.Game.Domain2.Tests.Matches.Services.Combat;

public sealed class AttackChoiceValidationServiceTests
{
    [Theory]
    [MatchAutoData]
    public void EnsureSubmittedActionIsValid_WhenContextIsNull_ThrowsArgumentNullException(
        CombatActionChoice choice,
        AttackChoiceValidationService sut)
    {
        CreaturePerspective ctx = null!;

        var act = () => sut.EnsureSubmittedActionIsValid(ctx, choice);

        act.Should()
            .Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("ctx");
    }

    [Theory]
    [MatchAutoData]
    public void EnsureSubmittedActionIsValid_WhenChoiceIsNull_ThrowsArgumentNullException(
        CreaturePerspective ctx,
        AttackChoiceValidationService sut)
    {
        CombatActionChoice choice = null!;

        var act = () => sut.EnsureSubmittedActionIsValid(ctx, choice);

        act.Should()
            .Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("choice");
    }

    [Theory]
    [MatchAutoData]
    public void EnsureSubmittedActionIsValid_WhenActionPolicyFails_ReturnsSameFailure(
        [Frozen] Mock<ICombatActionSelectionPolicy> actionPolicyMock,
        [Frozen] Mock<ICostPolicy> costPolicyMock,
        [Frozen] Mock<ITargetingPolicy> targetingPolicyMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        AttackChoiceValidationService sut)
    {
        const string errorCode = "SEL001_NOT_ALLOWED";

        actionPolicyMock
            .Setup(x => x.EnsureActionIsValid(ctx))
            .Returns(Result.Fail(errorCode));

        var result = sut.EnsureSubmittedActionIsValid(ctx, choice);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().Be(errorCode);

        costPolicyMock.Verify(x => x.EnsureCreatureHasEnoughEnergy(It.IsAny<CreaturePerspective>(), It.IsAny<Spell>()), Times.Never);
        targetingPolicyMock.Verify(x => x.EnsureCombatActionHasValidTargets(It.IsAny<CreaturePerspective>(), It.IsAny<CombatActionChoice>()), Times.Never);
    }

    [Theory]
    [MatchAutoData]
    public void EnsureSubmittedActionIsValid_WhenActionPolicyInvariantFails_ReturnsInvariantFailure(
        [Frozen] Mock<ICombatActionSelectionPolicy> actionPolicyMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        AttackChoiceValidationService sut)
    {
        const string errorCode = "IV001_CORRUPTED_STATE";

        actionPolicyMock
            .Setup(x => x.EnsureActionIsValid(ctx))
            .Returns(Result.InvariantFail(errorCode));

        var result = sut.EnsureSubmittedActionIsValid(ctx, choice);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be(errorCode);
    }

    [Theory]
    [MatchAutoData]
    public void EnsureSubmittedActionIsValid_WhenCostPolicyFails_ReturnsFailureAndSkipsTargeting(
        [Frozen] Mock<ICombatActionSelectionPolicy> actionPolicyMock,
        [Frozen] Mock<ICostPolicy> costPolicyMock,
        [Frozen] Mock<ITargetingPolicy> targetingPolicyMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        AttackChoiceValidationService sut)
    {
        actionPolicyMock
            .Setup(x => x.EnsureActionIsValid(ctx))
            .Returns(Result.Ok());

        const string errorCode = "E201_NOT_ENOUGH_ENERGY";

        costPolicyMock
            .Setup(x => x.EnsureCreatureHasEnoughEnergy(ctx, choice.SpellRef))
            .Returns(Result.Fail(errorCode));

        var result = sut.EnsureSubmittedActionIsValid(ctx, choice);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().Be(errorCode);

        targetingPolicyMock.Verify(
            x => x.EnsureCombatActionHasValidTargets(It.IsAny<CreaturePerspective>(), It.IsAny<CombatActionChoice>()),
            Times.Never);
    }

    [Theory]
    [MatchAutoData]
    public void EnsureSubmittedActionIsValid_WhenTargetingPolicyInvariantFails_ReturnsInvariantFailure(
        [Frozen] Mock<ICombatActionSelectionPolicy> actionPolicyMock,
        [Frozen] Mock<ICostPolicy> costPolicyMock,
        [Frozen] Mock<ITargetingPolicy> targetingPolicyMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        AttackChoiceValidationService sut)
    {
        actionPolicyMock
            .Setup(x => x.EnsureActionIsValid(ctx))
            .Returns(Result.Ok());

        costPolicyMock
            .Setup(x => x.EnsureCreatureHasEnoughEnergy(ctx, choice.SpellRef))
            .Returns(Result.Ok());

        const string errorCode = "IV_TGT_001";

        targetingPolicyMock
            .Setup(x => x.EnsureCombatActionHasValidTargets(ctx, choice))
            .Returns(Result<TargetingCheckReport>.InvariantFail(errorCode));

        var result = sut.EnsureSubmittedActionIsValid(ctx, choice);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be(errorCode);
    }

    [Theory]
    [MatchAutoData]
    public void EnsureSubmittedActionIsValid_WhenTargetingReportHasNoFailures_ReturnsOk(
        [Frozen] Mock<ICombatActionSelectionPolicy> actionPolicyMock,
        [Frozen] Mock<ICostPolicy> costPolicyMock,
        [Frozen] Mock<ITargetingPolicy> targetingPolicyMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        AttackChoiceValidationService sut)
    {
        actionPolicyMock
            .Setup(x => x.EnsureActionIsValid(ctx))
            .Returns(Result.Ok());

        costPolicyMock
            .Setup(x => x.EnsureCreatureHasEnoughEnergy(ctx, choice.SpellRef))
            .Returns(Result.Ok());

        var report = new TargetingCheckReport(Array.Empty<TargetingFailure>());

        targetingPolicyMock
            .Setup(x => x.EnsureCombatActionHasValidTargets(ctx, choice))
            .Returns(Result<TargetingCheckReport>.Ok(report));

        var result = sut.EnsureSubmittedActionIsValid(ctx, choice);

        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Theory]
    [MatchAutoData]
    public void EnsureSubmittedActionIsValid_WhenTargetingReportHasFailures_ShouldFailWithFirstErrorCode(
        [Frozen] Mock<ICombatActionSelectionPolicy> actionPolicyMock,
        [Frozen] Mock<ICostPolicy> costPolicyMock,
        [Frozen] Mock<ITargetingPolicy> targetingPolicyMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        AttackChoiceValidationService sut)
    {
        actionPolicyMock
            .Setup(x => x.EnsureActionIsValid(ctx))
            .Returns(Result.Ok());

        costPolicyMock
            .Setup(x => x.EnsureCreatureHasEnoughEnergy(ctx, choice.SpellRef))
            .Returns(Result.Ok());

        var failures = new[]
        {
            new TargetingFailure(null, "D402", "No targets provided."),
            new TargetingFailure(null, "D403", "Too many targets.")
        };

        var report = new TargetingCheckReport(failures);

        targetingPolicyMock
            .Setup(x => x.EnsureCombatActionHasValidTargets(ctx, choice))
            .Returns(Result<TargetingCheckReport>.Ok(report));

        var result = sut.EnsureSubmittedActionIsValid(ctx, choice);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().Be("D402");
    }
}

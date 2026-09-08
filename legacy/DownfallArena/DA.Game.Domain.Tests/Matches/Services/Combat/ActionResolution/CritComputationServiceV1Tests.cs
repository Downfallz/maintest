using AutoFixture;
using AutoFixture.Xunit2;
using DA.Game.Domain.Tests;
using DA.Game.Domain.Tests.Matches.Policies;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Services.Combat;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Resources.Stats;
using DA.Game.Shared.Utilities;
using FluentAssertions;
using Moq;
using Xunit;

namespace DA.Game.Domain2.Tests.Matches.Services.Combat;

public sealed class CritComputationServiceV1Tests
{
    private static CreaturePerspective WithActorCrit(
        CreaturePerspective ctx,
        double baseCrit,
        double bonusCrit)
    {
        var original = ctx.Creatures.First(c => c.CharacterId == ctx.ActorId);

        var cloned = CloneUtility.CloneSnapshot(
            original,
            baseCritical: CriticalChance.Of(baseCrit),
            bonusCritical: CriticalChance.Of(bonusCrit)
        );

        var updatedList = ctx.Creatures
            .Select(c => c.CharacterId == ctx.ActorId ? cloned : c)
            .ToList();

        return ctx with { Creatures = updatedList };
    }

    private static CombatActionChoice WithSpellCrit(
        CombatActionChoice choice,
        double spellCrit)
    {
        var newSpellRef = choice.SpellRef with
        {
            CritChance = CriticalChance.Of(spellCrit)
        };

        return choice with { SpellRef = newSpellRef };
    }

    // -----------------------------------------------------

    [Theory]
    [MatchAutoData]
    public void ApplyCrit_WhenChanceIsZero_ShouldNeverCrit(
        [Frozen] Mock<IRandom> rngMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        CritComputationServiceV1 sut)
    {
        rngMock.Setup(r => r.NextDouble()).Returns(0.0);

        ctx = WithActorCrit(ctx, baseCrit: 0.0, bonusCrit: 0.0);
        choice = WithSpellCrit(choice, spellCrit: 0.0);

        var result = sut.ApplyCrit(ctx, choice);

        result.IsCritical.Should().BeFalse();
        result.ChanceUsed.Should().Be(0.0);
        result.Roll.Should().Be(0.0);
    }

    // -----------------------------------------------------

    [Theory]
    [MatchAutoData]
    public void ApplyCrit_WhenChanceIsOne_ShouldAlwaysCrit(
        [Frozen] Mock<IRandom> rngMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        CritComputationServiceV1 sut)
    {
        rngMock.Setup(r => r.NextDouble()).Returns(0.9999);

        ctx = WithActorCrit(ctx, baseCrit: 0.5, bonusCrit: 0.25);
        choice = WithSpellCrit(choice, spellCrit: 0.25); // Total = 1.0

        var result = sut.ApplyCrit(ctx, choice);

        result.IsCritical.Should().BeTrue();
        result.ChanceUsed.Should().Be(1.0);
        result.Multiplier.Should().Be(2.0);
    }

    // -----------------------------------------------------

    [Theory]
    [MatchAutoData]
    public void ApplyCrit_WhenChanceExceedsOne_ShouldClampToOne(
        [Frozen] Mock<IRandom> rngMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        CritComputationServiceV1 sut)
    {
        rngMock.Setup(r => r.NextDouble()).Returns(0.3);

        ctx = WithActorCrit(ctx, baseCrit: 0.8, bonusCrit: 0.3);
        choice = WithSpellCrit(choice, spellCrit: 0.3); // Total = 1.4 -> clamp to 1

        var result = sut.ApplyCrit(ctx, choice);

        result.IsCritical.Should().BeTrue();
        result.ChanceUsed.Should().Be(1.0);
        result.Roll.Should().Be(0.3);
    }

    // -----------------------------------------------------

    [Theory]
    [MatchAutoData]
    public void ApplyCrit_WhenRollIsGreaterOrEqualThanChance_ShouldBeNormalHit(
        [Frozen] Mock<IRandom> rngMock,
        CreaturePerspective ctx,
        CombatActionChoice choice,
        CritComputationServiceV1 sut)
    {
        rngMock.Setup(r => r.NextDouble()).Returns(0.75);

        ctx = WithActorCrit(ctx, baseCrit: 0.3, bonusCrit: 0.1); // total = 0.4
        choice = WithSpellCrit(choice, spellCrit: 0.0);

        var result = sut.ApplyCrit(ctx, choice);

        result.IsCritical.Should().BeFalse();
        result.ChanceUsed.Should().Be(0.4);
        result.Roll.Should().Be(0.75);
    }
}

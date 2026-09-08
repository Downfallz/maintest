using DA.Game.Domain2.Matches.ValueObjects.Phases;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;
using FluentAssertions;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.ValueObjects.Phases;

public sealed class RoundLifecycleTests
{
    [Fact]
    public void Ctor_ShouldStartInStartOfRound()
    {
        // Act
        var lifecycle = new RoundLifecycle();

        // Assert
        lifecycle.Phase.Should().Be(RoundPhase.StartOfRound);
    }

    [Fact]
    public void MoveTo_Planning_FromStartOfRound_ShouldSucceed_AndUpdatePhase()
    {
        // Arrange
        var lifecycle = new RoundLifecycle();

        // Act
        var result = lifecycle.MoveTo(RoundPhase.Planning);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.Phase.Should().Be(RoundPhase.Planning);
    }

    [Fact]
    public void MoveTo_Combat_FromPlanning_ShouldSucceed_AndUpdatePhase()
    {
        // Arrange
        var lifecycle = new RoundLifecycle();
        lifecycle.MoveTo(RoundPhase.Planning).IsSuccess.Should().BeTrue();

        // Act
        var result = lifecycle.MoveTo(RoundPhase.Combat);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.Phase.Should().Be(RoundPhase.Combat);
    }

    [Fact]
    public void MoveTo_EndOfRound_FromCombat_ShouldSucceed_AndUpdatePhase()
    {
        // Arrange
        var lifecycle = new RoundLifecycle();
        lifecycle.MoveTo(RoundPhase.Planning);
        lifecycle.MoveTo(RoundPhase.Combat);

        // Act
        var result = lifecycle.MoveTo(RoundPhase.EndOfRound);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.Phase.Should().Be(RoundPhase.EndOfRound);
    }

    [Theory]
    [InlineData(RoundPhase.StartOfRound, RoundPhase.Combat)]      // skipping Planning
    [InlineData(RoundPhase.StartOfRound, RoundPhase.EndOfRound)]  // skipping both
    [InlineData(RoundPhase.Planning, RoundPhase.StartOfRound)]    // going backwards
    [InlineData(RoundPhase.Combat, RoundPhase.StartOfRound)]      // big jump backwards
    [InlineData(RoundPhase.Combat, RoundPhase.Planning)]          // backwards 1 step
    [InlineData(RoundPhase.EndOfRound, RoundPhase.Planning)]      // resurrect
    [InlineData(RoundPhase.EndOfRound, RoundPhase.Combat)]        // resurrect
    public void MoveTo_InvalidTransition_ShouldFail_AndKeepPhase(
        RoundPhase initial,
        RoundPhase next)
    {
        // Arrange
        var lifecycle = new RoundLifecycle();

        // Force initial phase
        MoveLifecycleTo(lifecycle, initial);
        var before = lifecycle.Phase;

        // Act
        var result = lifecycle.MoveTo(next);

        // Assert
        result.IsSuccess.Should().BeFalse();
        // If you went with InvariantFail:
        // result.IsInvariantFailure().Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();

        lifecycle.Phase.Should().Be(before);
    }

    [Fact]
    public void MoveTo_SamePhase_ShouldBeNoOpAndSuccess()
    {
        // Arrange
        var lifecycle = new RoundLifecycle();
        lifecycle.MoveTo(RoundPhase.Planning);

        // Act
        var result = lifecycle.MoveTo(RoundPhase.Planning);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.Phase.Should().Be(RoundPhase.Planning);
    }

    private static void MoveLifecycleTo(RoundLifecycle lifecycle, RoundPhase target)
    {
        // Minimal helper to bring lifecycle in the desired initial state
        switch (target)
        {
            case RoundPhase.StartOfRound:
                // default
                break;

            case RoundPhase.Planning:
                lifecycle.MoveTo(RoundPhase.Planning).IsSuccess.Should().BeTrue();
                break;

            case RoundPhase.Combat:
                lifecycle.MoveTo(RoundPhase.Planning).IsSuccess.Should().BeTrue();
                lifecycle.MoveTo(RoundPhase.Combat).IsSuccess.Should().BeTrue();
                break;

            case RoundPhase.EndOfRound:
                lifecycle.MoveTo(RoundPhase.Planning).IsSuccess.Should().BeTrue();
                lifecycle.MoveTo(RoundPhase.Combat).IsSuccess.Should().BeTrue();
                lifecycle.MoveTo(RoundPhase.EndOfRound).IsSuccess.Should().BeTrue();
                break;
        }
    }
}

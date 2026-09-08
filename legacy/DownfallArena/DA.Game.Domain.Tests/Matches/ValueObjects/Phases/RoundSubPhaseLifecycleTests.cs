using DA.Game.Domain2.Matches.ValueObjects.Phases;
using DA.Game.Shared.Contracts.Matches.Enums;
using FluentAssertions;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.ValueObjects.Phases;

public sealed class RoundSubPhaseLifecycleTests
{
    [Fact]
    public void InitializeForPhase_StartOfRound_ShouldSetFirstSubPhase()
    {
        // Arrange
        var lifecycle = new RoundSubPhaseLifecycle();

        // Act
        var result = lifecycle.InitializeForPhase(RoundPhase.StartOfRound);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.SubPhase.Should().Be(RoundSubPhase.Start_OngoingEffects);
    }

    [Fact]
    public void InitializeForPhase_Combat_ShouldSetIntentSelection()
    {
        // Arrange
        var lifecycle = new RoundSubPhaseLifecycle();

        // Act
        var result = lifecycle.InitializeForPhase(RoundPhase.Combat);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.SubPhase.Should().Be(RoundSubPhase.Combat_IntentSelection);
    }

    [Fact]
    public void MoveToNext_StartOfRoundFlow_ShouldAdvanceThroughFlow()
    {
        // Arrange
        var lifecycle = new RoundSubPhaseLifecycle();
        lifecycle.InitializeForPhase(RoundPhase.StartOfRound);

        // Act
        var firstNext = lifecycle.MoveToNext(RoundPhase.StartOfRound);

        // Assert
        firstNext.IsSuccess.Should().BeTrue();
        lifecycle.SubPhase.Should().Be(RoundSubPhase.Start_EnergyGain);
    }

    [Fact]
    public void MoveToNext_WhenAtLastSubPhase_ShouldFail()
    {
        // Arrange
        var lifecycle = new RoundSubPhaseLifecycle();
        lifecycle.InitializeForPhase(RoundPhase.StartOfRound);

        // Move to last subphase in this phase
        lifecycle.MoveToNext(RoundPhase.StartOfRound).IsSuccess.Should().BeTrue();
        lifecycle.SubPhase.Should().Be(RoundSubPhase.Start_EnergyGain);

        // Act
        var result = lifecycle.MoveToNext(RoundPhase.StartOfRound);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("I604");
        lifecycle.SubPhase.Should().Be(RoundSubPhase.Start_EnergyGain);
    }

    [Fact]
    public void MoveToNext_WhenNotInitialized_ShouldFail()
    {
        // Arrange
        var lifecycle = new RoundSubPhaseLifecycle();

        // Act
        var result = lifecycle.MoveToNext(RoundPhase.Combat);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("I602");
        lifecycle.SubPhase.Should().BeNull();
    }

    [Fact]
    public void MoveTo_ForwardInsideFlow_ShouldSucceed()
    {
        // Arrange
        var lifecycle = new RoundSubPhaseLifecycle();
        lifecycle.InitializeForPhase(RoundPhase.Planning);
        lifecycle.SubPhase.Should().Be(RoundSubPhase.Planning_Evolution);

        // Act
        var result = lifecycle.MoveTo(RoundPhase.Planning, RoundSubPhase.Planning_Speed);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.SubPhase.Should().Be(RoundSubPhase.Planning_Speed);
    }

    [Fact]
    public void MoveTo_BackwardsInsideFlow_ShouldFail()
    {
        // Arrange
        var lifecycle = new RoundSubPhaseLifecycle();
        lifecycle.InitializeForPhase(RoundPhase.Combat);

        // Move forward once
        lifecycle.MoveToNext(RoundPhase.Combat).IsSuccess.Should().BeTrue();
        lifecycle.SubPhase.Should().Be(RoundSubPhase.Combat_RevealAndTarget);

        // Act: try to go back to IntentSelection
        var result = lifecycle.MoveTo(RoundPhase.Combat, RoundSubPhase.Combat_IntentSelection);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("I607");
        lifecycle.SubPhase.Should().Be(RoundSubPhase.Combat_RevealAndTarget);
    }

    [Fact]
    public void MoveTo_SubPhaseNotInFlow_ShouldFail()
    {
        // Arrange
        var lifecycle = new RoundSubPhaseLifecycle();
        lifecycle.InitializeForPhase(RoundPhase.Combat);

        // Act: try to move to a Planning subphase while phase is Combat
        var result = lifecycle.MoveTo(RoundPhase.Combat, RoundSubPhase.Planning_Evolution);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("I605");
        lifecycle.SubPhase.Should().Be(RoundSubPhase.Combat_IntentSelection);
    }

    [Fact]
    public void MoveTo_FromNullToValidSubPhase_ShouldSetSubPhase()
    {
        // Arrange
        var lifecycle = new RoundSubPhaseLifecycle();

        // Act: no InitializeForPhase, we directly choose a valid subphase
        var result = lifecycle.MoveTo(RoundPhase.EndOfRound, RoundSubPhase.End_Cleanup);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.SubPhase.Should().Be(RoundSubPhase.End_Cleanup);
    }
}

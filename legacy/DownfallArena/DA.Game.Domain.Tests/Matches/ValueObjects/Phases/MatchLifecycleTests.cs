using DA.Game.Domain2.Matches.ValueObjects.Phases;
using DA.Game.Shared.Contracts.Matches.Enums;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Tests.Matches.ValueObjects.Phases;

public sealed class MatchLifecycleTests
{
    [Fact]
    public void Ctor_ShouldStartInWaitingForPlayers()
    {
        // Act
        var lifecycle = new MatchLifecycle();

        // Assert
        lifecycle.State.Should().Be(MatchState.WaitingForPlayers);
        lifecycle.IsWaitingForPlayers.Should().BeTrue();
        lifecycle.IsStarted.Should().BeFalse();
        lifecycle.HasEnded.Should().BeFalse();
    }

    [Fact]
    public void MoveTo_Started_FromWaitingForPlayers_ShouldSucceed_AndUpdateState()
    {
        // Arrange
        var lifecycle = new MatchLifecycle();

        // Act
        var result = lifecycle.MoveTo(MatchState.Started);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.State.Should().Be(MatchState.Started);
        lifecycle.IsStarted.Should().BeTrue();
        lifecycle.IsWaitingForPlayers.Should().BeFalse();
        lifecycle.HasEnded.Should().BeFalse();
    }

    [Fact]
    public void MoveTo_Ended_FromStarted_ShouldSucceed_AndUpdateState()
    {
        // Arrange
        var lifecycle = new MatchLifecycle();
        lifecycle.MoveTo(MatchState.Started).IsSuccess.Should().BeTrue();

        // Act
        var result = lifecycle.MoveTo(MatchState.Ended);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.State.Should().Be(MatchState.Ended);
        lifecycle.HasEnded.Should().BeTrue();
        lifecycle.IsStarted.Should().BeFalse();
        lifecycle.IsWaitingForPlayers.Should().BeFalse();
    }

    [Theory]
    [InlineData(MatchState.WaitingForPlayers, MatchState.Ended)]       // skip Started
    [InlineData(MatchState.Started, MatchState.WaitingForPlayers)]    // no rollback
    [InlineData(MatchState.Ended, MatchState.Started)]                // resurrect
    [InlineData(MatchState.Ended, MatchState.WaitingForPlayers)]      // full reset
    public void MoveTo_InvalidTransition_ShouldFail_AndKeepCurrentState(
        MatchState initial,
        MatchState next)
    {
        // Arrange
        var lifecycle = new MatchLifecycle();

        // Force initial state
        switch (initial)
        {
            case MatchState.Started:
                lifecycle.MoveTo(MatchState.Started);
                break;
            case MatchState.Ended:
                lifecycle.MoveTo(MatchState.Started);
                lifecycle.MoveTo(MatchState.Ended);
                break;
            case MatchState.WaitingForPlayers:
            default:
                // already default
                break;
        }

        var beforeState = lifecycle.State;

        // Act
        var result = lifecycle.MoveTo(next);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace(); // but we don't lock on exact text
        lifecycle.State.Should().Be(beforeState);
    }

    [Fact]
    public void HelperProperties_ShouldReflectCurrentState()
    {
        var lifecycle = new MatchLifecycle();

        // WaitingForPlayers
        lifecycle.IsWaitingForPlayers.Should().BeTrue();
        lifecycle.IsStarted.Should().BeFalse();
        lifecycle.HasEnded.Should().BeFalse();

        // Started
        lifecycle.MoveTo(MatchState.Started);
        lifecycle.IsWaitingForPlayers.Should().BeFalse();
        lifecycle.IsStarted.Should().BeTrue();
        lifecycle.HasEnded.Should().BeFalse();

        // Ended
        lifecycle.MoveTo(MatchState.Ended);
        lifecycle.IsWaitingForPlayers.Should().BeFalse();
        lifecycle.IsStarted.Should().BeFalse();
        lifecycle.HasEnded.Should().BeTrue();
    }
}


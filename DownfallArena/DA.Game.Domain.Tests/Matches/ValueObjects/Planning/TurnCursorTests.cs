using System;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using FluentAssertions;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.ValueObjects.Planning;

public sealed class TurnCursorTests
{
    [Fact]
    public void Start_ShouldHaveIndexZero()
    {
        // Act
        var cursor = TurnCursor.Start;

        // Assert
        cursor.Index.Should().Be(0);
    }

    [Fact]
    public void End_ShouldCreateCursorAtTotalSlots_AndBeEnd()
    {
        // Act
        var cursor = TurnCursor.End(5);

        // Assert
        cursor.Index.Should().Be(5);
        cursor.IsEnd(5).Should().BeTrue();
    }

    [Fact]
    public void End_WithNegativeTotalSlots_ShouldThrow()
    {
        // Act
        Action act = () => TurnCursor.End(-1);

        // Assert
        act.Should()
           .Throw<ArgumentOutOfRangeException>()
           .WithParameterName("totalSlots");
    }

    [Fact]
    public void IsEnd_WhenIndexLessThanTotalSlots_ShouldReturnFalse()
    {
        // Arrange
        var cursor = TurnCursor.Start.MoveNext(); // Index = 1

        // Act
        var isEnd = cursor.IsEnd(3);

        // Assert
        isEnd.Should().BeFalse();
    }

    [Fact]
    public void IsEnd_WhenIndexEqualsTotalSlots_ShouldReturnTrue()
    {
        // Arrange
        var cursor = TurnCursor.End(3); // Index = 3

        // Act
        var isEnd = cursor.IsEnd(3);

        // Assert
        isEnd.Should().BeTrue();
    }

    [Fact]
    public void IsEnd_WhenTotalSlotsNegative_ShouldThrow()
    {
        // Arrange
        var cursor = TurnCursor.Start;

        // Act
        Action act = () => cursor.IsEnd(-1);

        // Assert
        act.Should()
           .Throw<ArgumentOutOfRangeException>()
           .WithParameterName("totalSlots");
    }

    [Fact]
    public void MoveNext_ShouldIncrementIndex()
    {
        // Arrange
        var cursor = TurnCursor.Start; // 0

        // Act
        var next = cursor.MoveNext();

        // Assert
        next.Index.Should().Be(1);
    }

    [Fact]
    public void Reset_ShouldReturnStart()
    {
        // Arrange
        var cursor = TurnCursor.Start
            .MoveNext()
            .MoveNext(); // Index = 2

        // Act
        var reset = cursor.Reset();

        // Assert
        reset.Index.Should().Be(0);
    }

    [Fact]
    public void AdvanceIfPossible_WhenNotAtEnd_ShouldAdvance()
    {
        // Arrange
        var cursor = TurnCursor.Start; // 0

        // Act
        var advanced = cursor.AdvanceIfPossible(3);

        // Assert
        advanced.Index.Should().Be(1);
    }

    [Fact]
    public void AdvanceIfPossible_WhenAlreadyAtEnd_ShouldNotAdvance()
    {
        // Arrange
        var cursor = TurnCursor.End(3); // Index = 3

        // Act
        var advanced = cursor.AdvanceIfPossible(3);

        // Assert
        advanced.Index.Should().Be(3); // unchanged
        advanced.Should().Be(cursor);  // same value object
    }

    [Fact]
    public void ValueObject_Equality_ShouldBeBasedOnIndex()
    {
        // Arrange
        var a = TurnCursor.Start.AdvanceIfPossible(10); // 1
        var b = TurnCursor.Start.MoveNext();            // 1

        // Assert
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }
}

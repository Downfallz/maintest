using DA.Game.Shared.Contracts.Resources.Stats;
using FluentAssertions;

namespace DA.Game.Shared.Tests.Contracts.Resources.Stats;

public class EnergyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(27)]
    public void GivenValidValue_WhenCreatingEnergy_ThenStoresValue(int value)
    {
        var energy = Energy.Of(value);

        Assert.Equal(value, energy.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-3)]
    [InlineData(-42)]
    public void GivenNegativeValue_WhenCreatingEnergy_ThenThrows(int value)
    {
        Assert.Throws<ArgumentException>(() => Energy.Of(value));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(4, "4")]
    [InlineData(11, "11")]
    public void GivenEnergy_WhenCallingToString_ThenReturnsRawValue(int value, string expected)
    {
        var energy = Energy.Of(value);

        Assert.Equal(expected, energy.ToString());
    }

    [Fact]
    public void CompareTo_WhenOtherIsNull_ReturnsPositive()
    {
        // Arrange
        var energy = Energy.Of(10);

        // Act
        var result = energy.CompareTo(null);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_WhenThisIsLessThanOther_ReturnsNegative()
    {
        // Arrange
        var smaller = Energy.Of(5);
        var bigger = Energy.Of(10);

        // Act
        var result = smaller.CompareTo(bigger);

        // Assert
        result.Should().BeLessThan(0);
    }

    [Fact]
    public void CompareTo_WhenThisIsGreaterThanOther_ReturnsPositive()
    {
        // Arrange
        var smaller = Energy.Of(5);
        var bigger = Energy.Of(10);

        // Act
        var result = bigger.CompareTo(smaller);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_WhenValuesAreEqual_ReturnsZero()
    {
        // Arrange
        var e1 = Energy.Of(7);
        var e2 = Energy.Of(7);

        // Act
        var result = e1.CompareTo(e2);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void LessThanOperator_WhenLeftIsSmaller_ReturnsTrue()
    {
        // Arrange
        var left = Energy.Of(3);
        var right = Energy.Of(5);

        // Act
        var result = left < right;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void LessThanOperator_WhenLeftIsGreaterOrEqual_ReturnsFalse()
    {
        // Arrange
        var left = Energy.Of(5);
        var right = Energy.Of(5);
        var greater = Energy.Of(10);

        // Act & Assert
        (left < right).Should().BeFalse();
        (greater < right).Should().BeFalse();
    }

    [Fact]
    public void GreaterThanOperator_WhenLeftIsGreater_ReturnsTrue()
    {
        // Arrange
        var left = Energy.Of(10);
        var right = Energy.Of(5);

        // Act
        var result = left > right;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GreaterThanOperator_WhenLeftIsSmallerOrEqual_ReturnsFalse()
    {
        // Arrange
        var left = Energy.Of(5);
        var right = Energy.Of(5);
        var smaller = Energy.Of(3);

        // Act & Assert
        (left > right).Should().BeFalse();
        (smaller > right).Should().BeFalse();
    }

    [Fact]
    public void LessThanOrEqualOperator_HandlesEqualAndSmallerCorrectly()
    {
        // Arrange
        var smaller = Energy.Of(3);
        var equal1 = Energy.Of(5);
        var equal2 = Energy.Of(5);

        // Act & Assert
        (smaller <= equal1).Should().BeTrue();
        (equal1 <= equal2).Should().BeTrue();
    }

    [Fact]
    public void GreaterThanOrEqualOperator_HandlesEqualAndGreaterCorrectly()
    {
        // Arrange
        var greater = Energy.Of(10);
        var equal1 = Energy.Of(5);
        var equal2 = Energy.Of(5);

        // Act & Assert
        (greater >= equal1).Should().BeTrue();
        (equal1 >= equal2).Should().BeTrue();
    }

    [Fact]
    public void LessThanOperator_WhenLeftIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Energy? left = null;
        var right = Energy.Of(5);

        // Act
        var act = () => _ = left! < right;

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GreaterThanOperator_WhenLeftIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Energy? left = null;
        var right = Energy.Of(5);

        // Act
        var act = () => _ = left! > right;

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LessThanOrEqualOperator_WhenLeftIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Energy? left = null;
        var right = Energy.Of(5);

        // Act
        var act = () => _ = left! <= right;

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GreaterThanOrEqualOperator_WhenLeftIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Energy? left = null;
        var right = Energy.Of(5);

        // Act
        var act = () => _ = left! >= right;

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}

using DA.Game.Shared.Contracts.Resources.Stats;
using FluentAssertions;

namespace DA.Game.Shared.Tests.Contracts.Resources.Stats;

public class HealthTests
{
    // --- Creation ---

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(99)]
    public void GivenValidValue_WhenCreatingHealth_ThenStoresValue(int value)
    {
        var h = Health.Of(value);

        Assert.Equal(value, h.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    [InlineData(-42)]
    public void GivenNegativeValue_WhenCreatingHealth_ThenThrows(int value)
    {
        Assert.Throws<ArgumentException>(() => Health.Of(value));
    }


    // --- CompareTo ---

    [Fact]
    public void GivenHealth_WhenCompareToNull_ThenReturnsPositive()
    {
        var h = Health.Of(5);

        Assert.True(h.CompareTo(null) > 0);
    }

    [Fact]
    public void GivenTwoHealth_WhenCompareTo_ThenCorrectOrdering()
    {
        var low = Health.Of(3);
        var high = Health.Of(7);

        Assert.True(low.CompareTo(high) < 0);
        Assert.True(high.CompareTo(low) > 0);
        Assert.Equal(0, low.CompareTo(Health.Of(3)));
    }


    // --- Operators (<, >, <=, >=) ---

    [Fact]
    public void GivenTwoHealth_WhenUsingLessThanOperator_ThenWorks()
    {
        Assert.True(Health.Of(3) < Health.Of(5));
        Assert.False(Health.Of(5) < Health.Of(3));
    }

    [Fact]
    public void GivenTwoHealth_WhenUsingGreaterThanOperator_ThenWorks()
    {
        Assert.True(Health.Of(5) > Health.Of(3));
        Assert.False(Health.Of(3) > Health.Of(5));
    }

    [Fact]
    public void GivenTwoHealth_WhenUsingLessOrEqualOperator_ThenWorks()
    {
        Assert.True(Health.Of(3) <= Health.Of(3));
        Assert.True(Health.Of(3) <= Health.Of(4));
        Assert.False(Health.Of(4) <= Health.Of(3));
    }

    [Fact]
    public void GivenTwoHealth_WhenUsingGreaterOrEqualOperator_ThenWorks()
    {
        Assert.True(Health.Of(3) >= Health.Of(3));
        Assert.True(Health.Of(4) >= Health.Of(3));
        Assert.False(Health.Of(3) >= Health.Of(4));
    }


    // --- Null safety for comparison operators ---

    [Fact]
    public void GivenNullRightOperand_WhenUsingLessThanOperator_ThenThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            var _ = Health.Of(5) < null!;
        });
    }

    [Fact]
    public void GivenNullRightOperand_WhenUsingGreaterThanOperator_ThenThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            var _ = Health.Of(5) > null!;
        });
    }


    // --- IsDead() ---

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)] // même si invalid en création, test logique interne
    [InlineData(1, false)]
    [InlineData(10, false)]
    public void GivenHealth_WhenCheckingIsDead_ThenCorrect(bool expected, int value)
    {
        // On force un record même si valeur invalide pour tester la logique interne
        var h = new Health(value);

        Assert.Equal(expected, h.IsDead());
    }


    // --- ToString() ---

    [Theory]
    [InlineData(0, "0")]
    [InlineData(5, "5")]
    [InlineData(42, "42")]
    public void GivenHealth_WhenCallingToString_ThenReturnsValue(int value, string expected)
    {
        var h = Health.Of(value);

        Assert.Equal(expected, h.ToString());
    }

    [Fact]
    public void WithSubstracted_WhenAmountIsLessThanValue_ReturnsReducedHealth()
    {
        // Arrange
        var health = Health.Of(10);

        // Act
        var result = health.WithSubstracted(3);

        // Assert
        result.Value.Should().Be(7);
        result.IsDead().Should().BeFalse();
    }

    [Fact]
    public void WithSubstracted_WhenAmountEqualsValue_ReturnsZeroHealthAndIsDead()
    {
        // Arrange
        var health = Health.Of(5);

        // Act
        var result = health.WithSubstracted(5);

        // Assert
        result.Value.Should().Be(0);
        result.IsDead().Should().BeTrue();
    }

    [Fact]
    public void WithSubstracted_WhenAmountIsGreaterThanValue_ReturnsZeroHealthAndIsDead()
    {
        // Arrange
        var health = Health.Of(4);

        // Act
        var result = health.WithSubstracted(10);

        // Assert
        result.Value.Should().Be(0);
        result.IsDead().Should().BeTrue();
    }

    [Fact]
    public void WithSubstracted_WhenAmountIsNegative_ThrowsArgumentException()
    {
        // Arrange
        var health = Health.Of(10);

        // Act
        var act = () => health.WithSubstracted(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Substract amount must be >= 0*");
    }

    [Fact]
    public void WithAdded_WhenAmountIsZero_ReturnsSameValue()
    {
        // Arrange
        var health = Health.Of(10);

        // Act
        var result = health.WithAdded(0);

        // Assert
        result.Value.Should().Be(10);
        result.IsDead().Should().BeFalse();
    }

    [Fact]
    public void WithAdded_WhenAmountIsPositive_ReturnsIncreasedHealth()
    {
        // Arrange
        var health = Health.Of(7);

        // Act
        var result = health.WithAdded(5);

        // Assert
        result.Value.Should().Be(12);
        result.IsDead().Should().BeFalse();
    }

    [Fact]
    public void WithAdded_WhenCalledMultipleTimes_AccumulatesNaturally()
    {
        // Arrange
        var health = Health.Of(3);

        // Act
        var result = health.WithAdded(2).WithAdded(5);

        // Assert
        result.Value.Should().Be(10);
    }

    [Fact]
    public void WithAdded_WhenAmountIsNegative_ThrowsArgumentException()
    {
        // Arrange
        var health = Health.Of(10);

        // Act
        var act = () => health.WithAdded(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Added amount must be >= 0*");
    }
}

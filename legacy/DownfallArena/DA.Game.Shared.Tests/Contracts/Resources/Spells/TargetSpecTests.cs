using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using FluentAssertions;

namespace DA.Game.Shared.Tests.Contracts.Resources.Spells;

public class TargetingSpecTests
{
    [Fact]
    public void GivenValidParameters_WhenCreatingViaFactory_ThenPropertiesAreSet()
    {
        // Arrange
        var origin = TargetOrigin.Enemy;
        var scope = TargetScope.SingleTarget;
        int? maxTargets = 1;

        // Act
        var sut = TargetingSpec.Of(origin, scope, maxTargets);

        // Assert
        sut.Origin.Should().Be(origin);
        sut.Scope.Should().Be(scope);
        sut.MaxTargets.Should().Be(maxTargets);
    }

    [Fact]
    public void GivenNullMaxTargets_WhenCreating_ThenItIsAllowed()
    {
        // Arrange
        var origin = TargetOrigin.Ally;
        var scope = TargetScope.Multi;
        int? maxTargets = null;

        // Act
        var sut = TargetingSpec.Of(origin, scope, maxTargets);

        // Assert
        sut.MaxTargets.Should().BeNull();
    }

    [Fact]
    public void GivenInvalidMaxTargets_WhenCreating_ThenThrowsArgumentException()
    {
        // Arrange
        var origin = TargetOrigin.Enemy;
        var scope = TargetScope.SingleTarget;
        int? invalid = 0;

        // Act
        Action act = () => TargetingSpec.Of(origin, scope, invalid);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxTargets must be > 0*");
    }

    [Fact]
    public void GivenTwoSpecsWithSameValues_WhenComparing_ThenTheyAreEqual()
    {
        // Arrange
        var a = TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.Multi, 3);
        var b = TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.Multi, 3);

        // Act & Assert
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void GivenTwoSpecsWithDifferentValues_WhenComparing_ThenTheyAreNotEqual()
    {
        // Arrange
        var a = TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.SingleTarget, 1);
        var b = TargetingSpec.Of(TargetOrigin.Ally, TargetScope.Multi, 3);

        // Act & Assert
        a.Should().NotBe(b);
    }
}

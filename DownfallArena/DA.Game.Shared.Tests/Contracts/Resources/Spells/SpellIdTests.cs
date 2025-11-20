using DA.Game.Shared.Contracts.Resources.Spells;
using FluentAssertions;

namespace DA.Game.Shared.Tests.Contracts.Resources.Spells;

public class SpellIdTests {
    [Fact]
    public void GivenAName_WhenCreatingWithFactory_ThenNameIsSet()
    {
        // Arrange
        var name = "Fireball";

        // Act
        var sut = SpellId.New(name);

        // Assert
        sut.Name.Should().Be(name);
    }

    [Fact]
    public void GivenAName_WhenUsingConstructor_ThenNameIsSet()
    {
        // Arrange
        var name = "IceBolt";

        // Act
        var sut = new SpellId(name);

        // Assert
        sut.Name.Should().Be(name);
    }

    [Fact]
    public void GivenASpellId_WhenCallingToString_ThenReturnsName()
    {
        // Arrange
        var name = "ArcaneMissile";
        var sut = new SpellId(name);

        // Act
        var result = sut.ToString();

        // Assert
        result.Should().Be(name);
    }

    [Fact]
    public void GivenTwoSpellIdsWithSameName_WhenComparing_ThenTheyAreEqual()
    {
        // Arrange
        var a = new SpellId("Heal");
        var b = new SpellId("Heal");

        // Act & Assert
        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void GivenTwoSpellIdsWithDifferentNames_WhenComparing_ThenTheyAreNotEqual()
    {
        // Arrange
        var a = new SpellId("Shield");
        var b = new SpellId("Barrier");

        // Act & Assert
        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }
}

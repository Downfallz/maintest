using AutoFixture;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using FluentAssertions;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.ValueObjects.Evolution;

public class SpellUnlockChoiceTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void Of_WhenCharacterIdIsDefault_ShouldThrow()
    {
        // Arrange
        var id = default(CreatureId);
        var spell = _fixture.Create<Spell>();

        // Act
        var act = () => SpellUnlockChoice.Of(id, spell);

        // Assert
        act.Should()
           .Throw<ArgumentException>()
           .Which.ParamName.Should().Be("characterId");
    }

    [Fact]
    public void Of_WhenSpellIsNull_ShouldThrow()
    {
        // Arrange
        var id = new CreatureId(42);
        Spell spell = null!;

        // Act
        var act = () => SpellUnlockChoice.Of(id, spell);

        // Assert
        act.Should()
           .Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("spellRef");
    }

    [Fact]
    public void Of_WhenValid_ShouldCreateValueObject()
    {
        // Arrange
        var id = new CreatureId(42);
        var spell = _fixture.Create<Spell>();

        // Act
        var result = SpellUnlockChoice.Of(id, spell);

        // Assert
        result.CharacterId.Should().Be(id);
        result.SpellRef.Should().Be(spell);
    }

    [Fact]
    public void ValueObject_Equality_ShouldWork()
    {
        // Arrange
        var id = new CreatureId(42);
        var spell = _fixture.Create<Spell>();

        var one = SpellUnlockChoice.Of(id, spell);
        var two = SpellUnlockChoice.Of(id, spell);

        // Assert
        one.Should().Be(two);
        (one == two).Should().BeTrue();
    }
}

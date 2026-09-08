using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using FluentAssertions;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.ValueObjects.Planning;

public sealed class SpellUnlockChoiceTests
{
    [Theory, MatchAutoData]
    public void Of_WhenCharacterIdIsDefault_ShouldThrow(Spell spell)
    {
        // Arrange
        var id = default(CreatureId);

        // Act
        var act = () => SpellUnlockChoice.Of(id, spell);

        // Assert
        act.Should()
           .Throw<ArgumentException>()
           .Which.ParamName.Should().Be("characterId");
    }

    [Theory, MatchAutoData]
    public void Of_WhenSpellIsNull_ShouldThrow(CreatureId id)
    {
        // Arrange
        Spell spell = null!;

        // Act
        var act = () => SpellUnlockChoice.Of(id, spell);

        // Assert
        act.Should()
           .Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("spellRef");
    }

    [Theory, MatchAutoData]
    public void Of_WhenValid_ShouldCreateValueObject(CreatureId id, Spell spell)
    {
        // Act
        var result = SpellUnlockChoice.Of(id, spell);

        // Assert
        result.CharacterId.Should().Be(id);
        result.SpellRef.Should().Be(spell);
    }

    [Theory, MatchAutoData]
    public void ValueObject_Equality_ShouldBeValueBased(CreatureId id, Spell spell)
    {
        // Arrange
        var one = SpellUnlockChoice.Of(id, spell);
        var two = SpellUnlockChoice.Of(id, spell);

        // Assert
        one.Should().Be(two);
        (one == two).Should().BeTrue();
    }
}

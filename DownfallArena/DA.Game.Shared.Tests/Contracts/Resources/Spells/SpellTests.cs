using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Shared.Tests.Contracts.Resources.Spells;

public class SpellTests
{
    [Theory]
    [InlineGameAutoData("Fire Bolt")]
    public void GivenValidInputs_WhenCreatingSpell_ThenStoresValues(
        string name,
        SpellId id,
        SpellType spellType,
        CharClass characterClass,
        Initiative initiative,
        Energy energyCost,
        CriticalChance critChance,
        IReadOnlyCollection<IEffect> effects)
    {
        var spell = Spell.Create(
            id,
            name,
            spellType,
            characterClass,
            initiative,
            energyCost,
            critChance,
            effects
        );

        Assert.Equal(id, spell.Id);
        Assert.Equal(name, spell.Name);
        Assert.Equal(spellType, spell.SpellType);
        Assert.Equal(characterClass, spell.CharacterClass);
        Assert.Equal(initiative, spell.Initiative);
        Assert.Equal(energyCost, spell.EnergyCost);
        Assert.Equal(critChance, spell.CritChance);
        Assert.Same(effects, spell.Effects);
    }

    [Theory]
    [InlineGameAutoData("")]
    [InlineGameAutoData(" ")]
    [InlineGameAutoData("   ")]
    public void GivenInvalidName_WhenCreatingSpell_ThenThrows(
        string name,
        SpellId id,
        SpellType spellType,
        CharClass characterClass,
        Initiative initiative,
        Energy energyCost,
        CriticalChance critChance,
        IReadOnlyCollection<IEffect> effects)
    {
        Assert.Throws<ArgumentException>(() =>
            Spell.Create(
                id,
                name,
                spellType,
                characterClass,
                initiative,
                energyCost,
                critChance,
                effects));
    }

    [Theory]
    [GameAutoData]
    public void GivenNullEffects_WhenCreatingSpell_ThenThrows(
        SpellId id,
        SpellType spellType,
        CharClass characterClass,
        Initiative initiative,
        Energy energyCost,
        CriticalChance critChance,
        string name)
    {
        Assert.Throws<ArgumentException>(() =>
            Spell.Create(
                id,
                name,
                spellType,
                characterClass,
                initiative,
                energyCost,
                critChance,
                new List<Effect>()));
    }

    [Theory]
    [GameAutoData]
    public void GivenEmptyEffects_WhenCreatingSpell_ThenThrows(
        SpellId id,
        SpellType spellType,
        CharClass characterClass,
        Initiative initiative,
        Energy energyCost,
        CriticalChance critChance,
        string name)
    {
        var effects = Array.Empty<IEffect>();

        Assert.Throws<ArgumentException>(() =>
            Spell.Create(
                id,
                name,
                spellType,
                characterClass,
                initiative,
                energyCost,
                critChance,
                effects));
    }

    [Theory]
    [GameAutoData]
    public void GivenEffectsFromFixture_WhenCreatingSpell_ThenEachEffectHasNonNullTargeting(
        SpellId id,
        SpellType spellType,
        CharClass characterClass,
        Initiative initiative,
        Energy energyCost,
        CriticalChance critChance,
        string name,
        IReadOnlyCollection<IEffect> effects)
    {
        var spell = Spell.Create(
            id,
            name,
            spellType,
            characterClass,
            initiative,
            energyCost,
            critChance,
            effects
        );

        Assert.NotNull(spell.Effects);
        Assert.NotEmpty(spell.Effects);
        Assert.All(spell.Effects, e => Assert.NotNull(e.Targeting));
    }
}


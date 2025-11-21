using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Stats;
using DA.Game.Shared.Tests;
namespace DA.Game.Shared.Contracts.Tests.Resources.Creatures;

public class CharacterDefinitionRefTests
{
    [Theory]
    [InlineGameAutoData("Warrior")]
    public void GivenValidInputs_WhenCreatingCharacterDefinitionRef_ThenStoresValues(
        string name,
        CharacterDefId id,
        Health hp,
        Energy energy,
        Defense defense,
        Initiative initiative,
        CriticalChance crit,
        SpellId spell1,
        SpellId spell2)
    {
        var spells = new List<SpellId> { spell1, spell2 };

        var def = CharacterDefinitionRef.Create(
            id,
            name,
            hp,
            energy,
            defense,
            initiative,
            crit,
            spells
        );

        Assert.Equal(id, def.Id);
        Assert.Equal(name, def.Name);
        Assert.Equal(hp, def.BaseHp);
        Assert.Equal(energy, def.BaseEnergy);
        Assert.Equal(defense, def.BaseDefense);
        Assert.Equal(initiative, def.BaseInitiative);
        Assert.Equal(crit, def.BaseCritChance);
        Assert.Same(spells, def.StartingSpellIds);
    }

    [Theory]
    [InlineGameAutoData("")]
    [InlineGameAutoData(" ")]
    [InlineGameAutoData("   ")]
    public void GivenInvalidName_WhenCreatingCharacterDefinitionRef_ThenThrows(
        string name,
        CharacterDefId id,
        Health hp,
        Energy energy,
        Defense defense,
        Initiative initiative,
        CriticalChance crit,
        SpellId spell)
    {
        var spells = new List<SpellId> { spell };

        Assert.Throws<ArgumentException>(() =>
            CharacterDefinitionRef.Create(
                id,
                name,
                hp,
                energy,
                defense,
                initiative,
                crit,
                spells));
    }

    [Theory]
    [GameAutoData]
    public void GivenNullStartingSpellIds_WhenCreatingCharacterDefinitionRef_ThenThrows(
        CharacterDefId id,
        Health hp,
        Energy energy,
        Defense defense,
        Initiative initiative,
        CriticalChance crit)
    {
        Assert.Throws<ArgumentException>(() =>
            CharacterDefinitionRef.Create(
                id,
                "Warrior",
                hp,
                energy,
                defense,
                initiative,
                crit,
                new List<SpellId>()));
    }

    [Theory]
    [GameAutoData]
    public void GivenEmptyStartingSpellIds_WhenCreatingCharacterDefinitionRef_ThenThrows(
        CharacterDefId id,
        Health hp,
        Energy energy,
        Defense defense,
        Initiative initiative,
        CriticalChance crit)
    {
        var spells = new List<SpellId>();

        Assert.Throws<ArgumentException>(() =>
            CharacterDefinitionRef.Create(
                id,
                "Warrior",
                hp,
                energy,
                defense,
                initiative,
                crit,
                spells));
    }

    [Theory]
    [GameAutoData]
    public void GivenStartingSpellIdsWithDefaultSpellId_WhenCreatingCharacterDefinitionRef_ThenThrows(
        CharacterDefId id,
        Health hp,
        Energy energy,
        Defense defense,
        Initiative initiative,
        CriticalChance crit)
    {
        var spells = new List<SpellId> { default(SpellId) };

        Assert.Throws<ArgumentException>(() =>
            CharacterDefinitionRef.Create(
                id,
                "Warrior",
                hp,
                energy,
                defense,
                initiative,
                crit,
                spells));
    }
}
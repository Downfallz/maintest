using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Talents;
using DA.Game.Shared.Contracts.Resources.Stats;
using DA.Game.Shared.Tests;
namespace DA.Game.Shared.Contracts.Tests.Resources.Creatures;

public class CharacterDefinitionRefTests
{
    [Theory]
    [InlineGameAutoData("Warrior")]
    public void GivenValidInputs_WhenCreatingCharacterDefinitionRef_ThenStoresValues(
        string name,
        CreatureDefId id,
        Health hp,
        Energy energy,
        Defense defense,
        Initiative initiative,
        CriticalChance crit,
        TalentTreeId? talentTreeId,
        SpellId spell1,
        SpellId spell2)
    {
        var spells = new List<SpellId> { spell1, spell2 };

        var def = CreatureDefinitionRef.Create(
            id,
            name,
            hp,
            energy,
            defense,
            initiative,
            crit,
            talentTreeId,
            spells
        );

        Assert.Equal(id, def.Id);
        Assert.Equal(name, def.Name);
        Assert.Equal(hp, def.BaseHp);
        Assert.Equal(energy, def.BaseEnergy);
        Assert.Equal(defense, def.BaseDefense);
        Assert.Equal(initiative, def.BaseInitiative);
        Assert.Equal(crit, def.BaseCritChance);
        Assert.Equal(talentTreeId, def.TalentTreeId);
        Assert.Same(spells, def.StartingSpellIds);
    }

    [Theory]
    [InlineGameAutoData("")]
    [InlineGameAutoData(" ")]
    [InlineGameAutoData("   ")]
    public void GivenInvalidName_WhenCreatingCharacterDefinitionRef_ThenThrows(
        string name,
        CreatureDefId id,
        Health hp,
        Energy energy,
        Defense defense,
        Initiative initiative,
        CriticalChance crit,
        TalentTreeId? talentTreeId,
        SpellId spell)
    {
        var spells = new List<SpellId> { spell };

        Assert.Throws<ArgumentException>(() =>
            CreatureDefinitionRef.Create(
                id,
                name,
                hp,
                energy,
                defense,
                initiative,
                crit,
                talentTreeId,
                spells));
    }

    [Theory]
    [GameAutoData]
    public void GivenNullStartingSpellIds_WhenCreatingCharacterDefinitionRef_ThenThrows(
        CreatureDefId id,
        Health hp,
        Energy energy,
        Defense defense,
        Initiative initiative,
        TalentTreeId? talentTreeId,
        CriticalChance crit)
    {
        Assert.Throws<ArgumentException>(() =>
            CreatureDefinitionRef.Create(
                id,
                "Warrior",
                hp,
                energy,
                defense,
                initiative,
                crit,
                talentTreeId,
                new List<SpellId>()));
    }

    [Theory]
    [GameAutoData]
    public void GivenEmptyStartingSpellIds_WhenCreatingCharacterDefinitionRef_ThenThrows(
        CreatureDefId id,
        Health hp,
        Energy energy,
        Defense defense,
        Initiative initiative,
        CriticalChance crit,
        TalentTreeId? talentTreeId)
    {
        var spells = new List<SpellId>();

        Assert.Throws<ArgumentException>(() =>
            CreatureDefinitionRef.Create(
                id,
                "Warrior",
                hp,
                energy,
                defense,
                initiative,
                crit,
                talentTreeId,
                spells));
    }

    [Theory]
    [GameAutoData]
    public void GivenStartingSpellIdsWithDefaultSpellId_WhenCreatingCharacterDefinitionRef_ThenThrows(
        CreatureDefId id,
        Health hp,
        Energy energy,
        Defense defense,
        Initiative initiative,
        TalentTreeId? talentTreeId,
        CriticalChance crit)
    {
        var spells = new List<SpellId> { default(SpellId) };

        Assert.Throws<ArgumentException>(() =>
            CreatureDefinitionRef.Create(
                id,
                "Warrior",
                hp,
                energy,
                defense,
                initiative,
                crit,
                talentTreeId,
                spells));
    }
}
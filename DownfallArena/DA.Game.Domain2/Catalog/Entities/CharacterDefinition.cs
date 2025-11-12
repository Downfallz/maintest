using DA.Game.Domain2.Catalog.Ids;
using DA.Game.Domain2.Catalog.ValueObjects;
using DA.Game.Domain2.Catalog.ValueObjects.Character;
using DA.Game.Domain2.Shared.Primitives;
using System.Collections.Immutable;

namespace DA.Game.Domain2.Catalog.Entities;

public sealed class CharacterDefinition : Entity<CharacterDefId>
{
    // -------- State (lecture seule)
    public string Name { get; }
    public CharClass CharacterClass { get; }

    // Stats de base (VO)
    public Health BaseHealth { get; }
    public Energy BaseEnergy { get; }
    public Defense BaseDefense { get; }
    public Initiative BaseInitiative { get; }
    public CriticalChance BaseCriticalChance { get; }
    public Level Level { get; }  // si tu n’en as pas besoin, retire-le

    // Loadout initial (références vers le catalog de sorts)
    public ImmutableArray<SpellId> StartingSpellIds { get; }

    // -------- Ctor privé — pas de validation ici
    private CharacterDefinition(
        CharacterDefId id,
        string name,
        CharClass characterClass,
        Health baseHealth,
        Energy baseEnergy,
        Defense baseDefense,
        Initiative baseInitiative,
        CriticalChance baseCriticalChance,
        Level level,
        ImmutableArray<SpellId> startingSpellIds
    ) : base(id)
    {
        Name = name;
        CharacterClass = characterClass;
        BaseHealth = baseHealth;
        BaseEnergy = baseEnergy;
        BaseDefense = baseDefense;
        BaseInitiative = baseInitiative;
        BaseCriticalChance = baseCriticalChance;
        Level = level;
        StartingSpellIds = startingSpellIds.IsDefault ? [] : startingSpellIds;
    }

    // -------- Factory validante (point d’entrée unique)
    public static CharacterDefinition Create(
        CharacterDefId id,
        string name,
        CharClass characterClass,
        Health baseHealth,                 // ex: Health.Of(100) (Max=100)
        Energy baseEnergy,                 // ex: Energy.Of(6, max:10) ou Energy.Of(6) si max fixe
        Defense baseDefense,               // ex: Defense.Of(2)
        Initiative baseInitiative,         // ex: Initiative.Of(5)
        CriticalChance baseCriticalChance, // ex: CriticalChance.Of(0.15)
        Level level,                       // ex: Level.Of(1)
        IEnumerable<SpellId> startingSpellIds
    )
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException("name");
        
        var loadout = (startingSpellIds ?? Array.Empty<SpellId>()).ToImmutableArray();
        if (loadout.Length == 0)
            throw new ArgumentException("Character must have at least one starting spell");

        return new CharacterDefinition(
            id, name, characterClass,
            baseHealth, baseEnergy, baseDefense, baseInitiative, baseCriticalChance, level,
            loadout
        );
    }

    // -------- Aides immuables ciblées (optionnelles)
    public CharacterDefinition WithStartingSpells(IEnumerable<SpellId> spellIds)
        => new(Id, Name, CharacterClass, BaseHealth, BaseEnergy, BaseDefense, BaseInitiative, BaseCriticalChance, Level,
               (spellIds ?? Array.Empty<SpellId>()).ToImmutableArray());

    public CharacterDefinition WithBaseStats(
        Health? health = null, Energy? energy = null, Defense? defense = null,
        Initiative? initiative = null, CriticalChance? crit = null, Level? level = null)
        => new(Id, Name, CharacterClass,
               health ?? BaseHealth,
               energy ?? BaseEnergy,
               defense ?? BaseDefense,
               initiative ?? BaseInitiative,
               crit ?? BaseCriticalChance,
               level ?? Level,
               StartingSpellIds);

    public override string ToString()
        => $"{Name} [{CharacterClass}] HP:{BaseHealth.Value:0.#} EN:{BaseEnergy.Value:0.#} " +
           $"Def:{BaseDefense.Value:0.#} Init:{BaseInitiative.Value} Crit:{BaseCriticalChance.Value:P0} " +
           $"Spells:{StartingSpellIds.Length}";
}

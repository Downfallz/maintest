using DA.Game.Domain2.Catalog.ValueObjects.Spells;
using DA.Game.Domain2.Catalog.ValueObjects.Stats;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Catalog.Enums;
using DA.Game.Shared.Contracts.Catalog.Ids;
using System.Collections.Immutable;

namespace DA.Game.Domain2.Catalog.Entities;

public sealed class SpellDefinition : Entity<SpellId>
{
    // --------- State (lecture seule à l'extérieur)
    public string Name { get; }
    public SpellType SpellType { get; }
    public CharClass CharacterClass { get; }
    public Initiative Initiative { get; }

    public Energy? EnergyCost { get; }
    public CriticalChance? CriticalChance { get; }

    public ImmutableArray<Effect> Effects { get; }

    // --------- Ctor privé : aucune validation ici (uniquement construction)
    private SpellDefinition(
        SpellId id,
        string name,
        SpellType spellType,
        CharClass characterClass,
        Initiative initiative,
        Energy? energyCost,
        CriticalChance? criticalChance,
        ImmutableArray<Effect> effects) : base(id)
    {
        Name = name;
        SpellType = spellType;
        CharacterClass = characterClass;
        Initiative = initiative;
        EnergyCost = energyCost;
        CriticalChance = criticalChance;
        Effects = effects.IsDefault ? [] : effects;
    }

    // --------- Factory validante : point d’entrée unique
    public static SpellDefinition Create(
        SpellId id,
        string name,
        SpellType spellType,
        CharClass characterClass,
        Initiative initiative,
        Energy? energyCost = null,
        CriticalChance? criticalChance = null,
        IEnumerable<Effect>? effects = null)
    {
        // Garde-fous métier “au bord”
        
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("A spell must have a non-empty name.", nameof(name));

        var eff = (effects ?? Array.Empty<Effect>()).ToImmutableArray();
        if (eff.Any(e => e is null))
            throw new ArgumentException("A spell cannot have null Effects.", nameof(effects));
        if (eff.Length == 0)
            throw new ArgumentException("A spell must define at least one Effect.", nameof(effects));

        return new SpellDefinition(id, name, spellType, characterClass, initiative, energyCost, criticalChance, eff);
    }

    // --------- “Withers” immuables (reviennent avec une nouvelle instance)
    public SpellDefinition WithEnergyCost(Energy? energy) =>
        new(Id, Name, SpellType, CharacterClass, Initiative, energy, CriticalChance, Effects);

    public SpellDefinition WithCriticalChance(CriticalChance? crit) =>
        new(Id, Name, SpellType, CharacterClass, Initiative, EnergyCost, crit, Effects);

    public SpellDefinition WithInitiative(Initiative initiative) =>
        new(Id, Name, SpellType, CharacterClass, initiative, EnergyCost, CriticalChance, Effects);

    public SpellDefinition WithEffects(IEnumerable<Effect> effects) =>
        new(Id, Name, SpellType, CharacterClass, Initiative, EnergyCost, CriticalChance, effects.ToImmutableArray());

    public override string ToString()
        => $"{Name} [{SpellType}/{CharacterClass}] cost:{EnergyCost?.Value.ToString() ?? "-"} " +
           $"init:{Initiative.Value} crit:{(CriticalChance is null ? "-" : $"{CriticalChance.Value:P0}")} " +
           $"effects:{(Effects.Length == 0 ? "-" : string.Join('|', Effects.Select(e => e.GetType().Name)))}";
}

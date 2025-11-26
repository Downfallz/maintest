using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Shared.Contracts.Resources.Spells;

public sealed record Spell(
    SpellId Id,
    string Name,
    SpellType SpellType,
    CreatureClass CreatureClass,
    Initiative Initiative,
    Energy EnergyCost,
    CriticalChance CritChance,
    TargetingSpec TargetingSpec,
    IReadOnlyCollection<IEffect> Effects)
{
    public static Spell Create(
        SpellId id,
        string name,
        SpellType spellType,
        CreatureClass classType,
        Initiative initiative,
        Energy energyCost,
        CriticalChance critChance,
        TargetingSpec targetingSpec,
        IReadOnlyCollection<IEffect> effects)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Spell name cannot be empty.", nameof(name));

        if (effects is null || effects.Count == 0)
            throw new ArgumentException("Spell must have at least one effect.", nameof(effects));

        return new Spell(
            id,
            name,
            spellType,
            classType,
            initiative,
            energyCost,
            critChance,
            targetingSpec,
            effects
        );
    }
}
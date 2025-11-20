using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Shared.Contracts.Resources.Spells;

public sealed record Spell(
    SpellId Id,
    string Name,
    SpellType SpellType,
    CharClass CharacterClass,
    Initiative Initiative,
    Energy EnergyCost,
    CriticalChance CritChance,
    IReadOnlyCollection<IEffect> Effects
);
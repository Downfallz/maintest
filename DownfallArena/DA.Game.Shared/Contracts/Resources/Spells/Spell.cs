using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Contracts.Resources.Spells;

public sealed record Spell(
    SpellId Id,
    string Name,
    SpellType SpellType,
    CharClass CharacterClass,
    int Initiative,
    int EnergyCost,
    double CritChance,
    IReadOnlyCollection<IEffect> Effects
);
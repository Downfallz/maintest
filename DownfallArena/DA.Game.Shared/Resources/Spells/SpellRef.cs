using DA.Game.Shared.Contracts.Catalog.Enums;
using DA.Game.Shared.Contracts.Catalog.Ids;
using DA.Game.Shared.Resources.Spells.Effects;

namespace DA.Game.Shared.Resources.Spells;

public sealed record SpellRef(
    SpellId Id,
    string Name,
    SpellType SpellType,
    CharClass CharacterClass,
    int Initiative,
    int EnergyCost,
    double CritChance,
    IEffect[] Effects
);
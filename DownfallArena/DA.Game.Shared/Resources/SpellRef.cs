using DA.Game.Domain2.Catalog.Ids;
using DA.Game.Domain2.Catalog.ValueObjects.Spells;
using DA.Game.Resources.Enums;

namespace DA.Game.Domain2.Matches.Resources;

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
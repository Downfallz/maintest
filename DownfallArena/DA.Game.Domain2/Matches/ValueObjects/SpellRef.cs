using DA.Game.Domain2.Catalog.Ids;

namespace DA.Game.Domain2.Match.ValueObjects;

public sealed record SpellRef(
    SpellId Id,
    string Name,
    int EnergyCost,
    int Power,
    double CritChance
);

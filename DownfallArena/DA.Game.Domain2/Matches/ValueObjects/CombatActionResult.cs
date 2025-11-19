using DA.Game.Domain2.Catalog.Ids;
using DA.Game.Domain2.Shared.Ids;

namespace DA.Game.Domain2.Matches.ValueObjects;
public sealed record CombatActionResult(
    CombatActionChoice choice,
    IReadOnlyList<EffectSummary> Effects);

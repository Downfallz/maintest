using DA.Game.Domain2.Catalog.Ids;
using DA.Game.Domain2.Shared.Ids;

namespace DA.Game.Domain2.Matches.ValueObjects;
public sealed record CombatActionRequest(
    CharacterId ActorId,
    SpellId SpellId,
    IReadOnlyList<CharacterId> TargetIds,
    string Source);

using DA.Game.Domain2.Catalog.Ids;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Matches.Resources;
using DA.Game.Domain2.Shared.Ids;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.ValueObjects;
public sealed record CombatActionChoice(
    CharacterId ActorId,
    SpellRef SpellRef,
    IReadOnlyList<CharacterId> TargetIds) : ValueObject
{
    public static CombatActionChoice Create(CharacterId id, SpellRef spellRef, IReadOnlyList<CharacterId> TargetIds)
    {
        return new CombatActionChoice(id, spellRef, TargetIds);
    }
}

using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Resources.Spells;

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

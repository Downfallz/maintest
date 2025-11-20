using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record CombatActionChoice(
    CharacterId ActorId,
    Spell SpellRef,
    IReadOnlyList<CharacterId> TargetIds) : ValueObject
{
    public static CombatActionChoice Create(CharacterId id, Spell spellRef, IReadOnlyList<CharacterId> TargetIds)
    {
        return new CombatActionChoice(id, spellRef, TargetIds);
    }
}

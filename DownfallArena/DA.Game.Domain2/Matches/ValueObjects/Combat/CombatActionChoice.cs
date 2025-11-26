using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects.Combat;

public sealed record CombatActionChoice(
    CreatureId ActorId,
    Spell SpellRef,
    IReadOnlyList<CreatureId> TargetIds) : ValueObject
{
    public static CombatActionChoice Create(CreatureId id, Spell spellRef, IReadOnlyList<CreatureId> TargetIds)
    {
        return new CombatActionChoice(id, spellRef, TargetIds);
    }
}

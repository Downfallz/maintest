using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects.Combat;

public sealed record CombatActionIntent(
    CreatureId ActorId,
    Spell SpellRef) : ValueObject
{
    public static CombatActionIntent Create(CreatureId id, Spell spellRef)
    {
        return new CombatActionIntent(id, spellRef);
    }
}

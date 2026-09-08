using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects.Combat;

/// <summary>
/// Represents the spell chosen by the creature BEFORE selecting targets.
/// </summary>
public sealed record CombatActionIntent(
    CreatureId ActorId,
    Spell SpellRef) : ValueObject
{
    private const string I701_ACTOR_ID_INVALID =
        "I701 - CombatActionIntent requires a valid CreatureId.";

    private const string I702_SPELL_REF_REQUIRED =
        "I702 - CombatActionIntent requires a non-null SpellRef.";

    public static CombatActionIntent Create(CreatureId id, Spell spellRef)
    {
        // Validate inputs → invariant failures, since an intent
        // should NEVER be created with invalid data.

        if (id.Value <= 0)
            throw new ArgumentException(I701_ACTOR_ID_INVALID, nameof(id));

        if (spellRef is null)
            throw new ArgumentNullException(nameof(spellRef), I702_SPELL_REF_REQUIRED);

        return new CombatActionIntent(id, spellRef);
    }
}

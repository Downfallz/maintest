using System;
using System.Collections.Generic;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects.Combat;

/// <summary>
/// Represents a fully bound combat action:
/// - Who acts (ActorId)
/// - Which spell is used (SpellRef)
/// - Which creatures are targeted (TargetIds)
/// </summary>
public sealed record CombatActionChoice(
    CreatureId ActorId,
    Spell SpellRef,
    IReadOnlyList<CreatureId> TargetIds) : ValueObject
{
    private const string I711_ACTOR_ID_INVALID =
        "I711 - CombatActionChoice requires a valid CreatureId.";

    private const string I712_SPELL_REF_REQUIRED =
        "I712 - CombatActionChoice requires a non-null SpellRef.";

    private const string I713_TARGET_IDS_REQUIRED =
        "I713 - CombatActionChoice requires a non-null TargetIds collection.";

    public static CombatActionChoice Create(
        CreatureId actorId,
        Spell spellRef,
        IReadOnlyList<CreatureId> targetIds)
    {
        if (actorId.Value <= 0)
            throw new ArgumentException(I711_ACTOR_ID_INVALID, nameof(actorId));

        if (spellRef is null)
            throw new ArgumentNullException(nameof(spellRef), I712_SPELL_REF_REQUIRED);

        if (targetIds is null)
            throw new ArgumentNullException(nameof(targetIds), I713_TARGET_IDS_REQUIRED);

        return new CombatActionChoice(actorId, spellRef, targetIds);
    }

    public static CombatActionChoice FromIntentAndTargets(
        CombatActionIntent intent,
        IReadOnlyList<CreatureId> targetIds)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (targetIds is null)
            throw new ArgumentNullException(nameof(targetIds), I713_TARGET_IDS_REQUIRED);

        return Create(intent.ActorId, intent.SpellRef, targetIds);
    }
}

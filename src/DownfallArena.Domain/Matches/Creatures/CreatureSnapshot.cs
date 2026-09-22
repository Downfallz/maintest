using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Matches.Creatures;

/// <summary>
/// Immutable copy of a creature's state, handed to rules and projections instead of the entity.
/// </summary>
public sealed record CreatureSnapshot
{
    public required CreatureId Id { get; init; }

    public required PlayerSlot Owner { get; init; }

    public required CreatureDefinitionId DefinitionId { get; init; }

    public required string Name { get; init; }

    public required TalentTreeId TalentTree { get; init; }

    public required Health Health { get; init; }

    public required Health MaxHealth { get; init; }

    public required Energy Energy { get; init; }

    public required Defense TotalDefense { get; init; }

    /// <summary>The creature's own initiative before conditions, raised by everything it has unlocked.</summary>
    public required Initiative BaseInitiative { get; init; }

    /// <summary>The base initiative less the active debuffs: what the timeline orders on.</summary>
    public required Initiative CurrentInitiative { get; init; }

    public required CriticalChance CriticalChance { get; init; }

    public required bool IsStunned { get; init; }

    public required IReadOnlySet<SpellId> KnownSpells { get; init; }

    /// <summary>
    /// The packages this creature has bought. Held rather than inferred from <see cref="KnownSpells"/>: a
    /// starting kit, a spell two packages could teach, and a match restored from an older shape all make the
    /// spells an unreliable witness to what was purchased.
    /// </summary>
    public IReadOnlySet<TierId> AcquiredTiers { get; init; } = new HashSet<TierId>();

    public required IReadOnlyList<ConditionSnapshot> Conditions { get; init; }

    public bool IsDead => Health.IsZero;

    public bool IsAlive => !IsDead;

    public bool KnowsSpell(SpellId spellId) => KnownSpells.Contains(spellId);

    public bool OwnsTier(TierId tierId) => AcquiredTiers.Contains(tierId);
}

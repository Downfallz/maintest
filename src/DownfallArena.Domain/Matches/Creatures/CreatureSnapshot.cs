using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
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

    /// <summary>
    /// Rounds left of the immunity a stun leaves behind (ADR 0072): one through the round after a stun ends,
    /// zero otherwise. Defaults to zero, which is every creature a snapshot was taken of before the rule.
    /// </summary>
    public int StunImmunityRounds { get; init; }

    /// <summary>What the packages it owns give it for good (ADR 0101), combined; <see cref="Passive.None"/> unless it owns one that gives something.</summary>
    public Passive Passive { get; init; } = Passive.None;

    /// <summary>Whether a stun would be ignored for immunity. A stunned creature ignores one too; <see cref="CanBeStunned"/> says both.</summary>
    public bool IsStunImmune => IsAlive && (StunImmunityRounds > 0 || Passive.StunImmunity);

    /// <summary>Whether a stun cast on this creature would land: alive, and neither stunned nor immune (ADR 0072).</summary>
    public bool CanBeStunned => IsAlive && !IsStunned && !IsStunImmune;

    /// <summary>Added to every direct hit it deals: its packages' bonus and its damage buffs (ADR 0101).</summary>
    public int DamageBonus
    {
        get
        {
            // A loop rather than a query: the agents read this once for every enemy of every board they score.
            var bonus = Passive.DamageBonus;
            foreach (var condition in Conditions)
            {
                if (condition.Effect is DamageBuff buff)
                {
                    bonus = checked(bonus + buff.Amount);  // as Sum did: an overflow is a broken invariant, not a wrap
                }
            }

            return bonus;
        }
    }

    public bool IsDead => Health.IsZero;

    public bool IsAlive => !IsDead;

    public bool KnowsSpell(SpellId spellId) => KnownSpells.Contains(spellId);

    public bool OwnsTier(TierId tierId) => AcquiredTiers.Contains(tierId);
}

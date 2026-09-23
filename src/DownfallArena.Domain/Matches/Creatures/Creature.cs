using System.Collections.ObjectModel;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Matches.Creatures;

/// <summary>
/// A combat unit in a match, spawned from a creature definition. Every state change goes through a method that
/// protects the invariants; derived values (stun, total defense, current initiative) come from the conditions.
/// The mutators are internal: only the <see cref="Match"/> aggregate and the rules it runs may change a creature,
/// and <see cref="Rules.Advance"/>, on creatures it restores and never hands out (ADR 0047).
/// </summary>
public sealed class Creature : Entity<CreatureId>
{
    private readonly HashSet<SpellId> _knownSpells;
    private readonly HashSet<TierId> _acquiredTiers;

    // Handed out in place of the sets themselves. IReadOnlySet<T> on a HashSet<T> is one cast away from
    // ISet<T>, so a caller holding a creature could teach it a spell or grant it a package without the method
    // that protects the invariant -- which is exactly the drift ownership is held to prevent. The wrapper is
    // built once over the live set, so a read costs nothing and still sees every change the mutators make.
    private readonly ReadOnlySet<SpellId> _knownSpellsView;
    private readonly ReadOnlySet<TierId> _acquiredTiersView;

    private readonly ConditionSet _conditions;

    /// <summary>
    /// Rounds left of the immunity a stun leaves behind (ADR 0072). A state of the creature and not a condition:
    /// no spell carries it, no source is charged for it, and the rules alone start and end it.
    /// </summary>
    private int _stunImmunity;

    /// <summary>How long the immunity a stun leaves behind lasts: the one round after it ends (ADR 0072).</summary>
    private const int StunImmunityDuration = 1;

    private Creature(CreatureId id, PlayerSlot owner, CreatureDefinition definition)
        : base(id)
    {
        Owner = owner;
        Definition = definition;
        Health = definition.BaseStats.Health;
        Energy = definition.BaseStats.Energy;
        BaseInitiative = definition.BaseStats.Initiative;
        _knownSpells = [.. definition.StartingSpells];
        _acquiredTiers = [];
        _knownSpellsView = new ReadOnlySet<SpellId>(_knownSpells);
        _acquiredTiersView = new ReadOnlySet<TierId>(_acquiredTiers);
        _conditions = new ConditionSet();
    }

    private Creature(CreatureSnapshot snapshot, CreatureDefinition definition)
        : base(snapshot.Id)
    {
        Owner = snapshot.Owner;
        Definition = definition;
        Health = snapshot.Health;
        Energy = snapshot.Energy;
        BaseInitiative = snapshot.BaseInitiative;
        _knownSpells = [.. snapshot.KnownSpells];

        // The snapshot's base initiative already includes every bonus the creature bought, so restoring the
        // tiers must not add them again. The list is what it owns, not a script to replay.
        _acquiredTiers = [.. snapshot.AcquiredTiers];
        _knownSpellsView = new ReadOnlySet<SpellId>(_knownSpells);
        _acquiredTiersView = new ReadOnlySet<TierId>(_acquiredTiers);
        _conditions = new ConditionSet(snapshot.Conditions);
        _stunImmunity = snapshot.StunImmunityRounds;
    }

    public PlayerSlot Owner { get; }

    public CreatureDefinition Definition { get; }

    public string Name => Definition.Name;

    /// <summary>
    /// The definition's block. Private: its Initiative is the value this creature spawned with, not the one it
    /// carries — that is <see cref="BaseInitiative"/> — and the rest reaches callers through the named values.
    /// </summary>
    private CreatureStats BaseStats => Definition.BaseStats;

    public Health Health { get; private set; }

    public Health MaxHealth => BaseStats.Health;

    public Energy Energy { get; private set; }

    public IReadOnlySet<SpellId> KnownSpells => _knownSpellsView;

    /// <summary>The packages this creature has bought, in no particular order.</summary>
    public IReadOnlySet<TierId> AcquiredTiers => _acquiredTiersView;

    public IReadOnlyList<Condition> Conditions => _conditions.Active;

    public bool IsDead => Health.IsZero;

    public bool IsAlive => !IsDead;

    public bool IsStunned => IsAlive && _conditions.Has<Stun>();

    /// <summary>Whether a stun would be ignored: the round after a stun ends (ADR 0072).</summary>
    public bool IsStunImmune => IsAlive && _stunImmunity > 0;

    public Defense TotalDefense => BaseStats.Defense
        .Plus(_conditions.Sum<DefenseBuff>(buff => buff.Amount))
        .Minus(_conditions.Sum<DefenseDebuff>(debuff => debuff.Amount));

    /// <summary>
    /// The creature's own initiative, before any condition: the definition's, raised for good by the bonus of
    /// every package it has bought in this match (ADR 0056). The starting kit is part of the definition's
    /// block and is not a package, so it raises nothing.
    /// </summary>
    public Initiative BaseInitiative { get; private set; }

    /// <summary>
    /// The base initiative plus the active initiative buffs and less the debuffs, which is what orders the
    /// timeline. Buffs are added first so the floor at zero applies to the total, not to an intermediate
    /// (ADR 0036) -- the same order <see cref="TotalDefense"/> uses.
    /// </summary>
    public Initiative CurrentInitiative => BaseInitiative
        .Plus(_conditions.Sum<InitiativeBuff>(buff => buff.Amount))
        .Minus(_conditions.Sum<InitiativeDebuff>(debuff => debuff.Amount));

    public CriticalChance CriticalChance => BaseStats.CriticalChance;

    public static Creature Spawn(CreatureId id, PlayerSlot owner, CreatureDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new Creature(id, owner, definition);
    }

    /// <summary>
    /// The creature a snapshot was taken of, at that state: health, energy, base initiative, known spells and
    /// conditions as they were, so the rules a match runs can be run on it. Internal on purpose, and the one
    /// door into a creature that did not spawn at full health: <see cref="Rules.Advance"/> uses it to answer
    /// what a board would be after a move a match has not played (ADR 0047), and nothing else does. A snapshot
    /// is a copy of a real creature, so its health cannot exceed the definition's and it knows its starting
    /// spells and nothing but those and what its packages taught it; a caller that hands one where that fails
    /// has a bug, not a rule violation.
    /// </summary>
    /// <param name="acquired">
    /// The packages <paramref name="snapshot"/> says it owns, resolved from the catalogue by the caller. It is
    /// asked for rather than looked up so that a snapshot naming a package nobody authored cannot be restored
    /// at all, and because <see cref="Creature"/> has no catalogue of its own.
    /// </param>
    internal static Creature Restore(CreatureSnapshot snapshot, CreatureDefinition definition, IReadOnlyList<Tier> acquired)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(acquired);

        if (definition.Id != snapshot.DefinitionId)
        {
            throw new ArgumentException($"Creature {snapshot.Id} was spawned from '{snapshot.DefinitionId.Value}', not '{definition.Id.Value}'.", nameof(definition));
        }

        if (acquired.Count != snapshot.AcquiredTiers.Count || !acquired.All(tier => snapshot.OwnsTier(tier.Id)))
        {
            throw new ArgumentException($"Creature {snapshot.Id} owns {snapshot.AcquiredTiers.Count} package(s), and {acquired.Count} other one(s) were handed in.", nameof(acquired));
        }

        if (snapshot.Health > definition.BaseStats.Health)
        {
            throw new ArgumentException($"Creature {snapshot.Id} cannot have {snapshot.Health} health out of {definition.BaseStats.Health}.", nameof(snapshot));
        }

        // What a creature knows is what it spawned with plus what its packages taught it (ADR 0056). A spell
        // from anywhere else would let the hypothetical board cast past the evolution rules. The talent tree
        // used to be the third source here, and is not one any more: it gates nothing a pick buys.
        var taught = acquired.SelectMany(tier => tier.Spells).ToHashSet();
        if (!definition.StartingSpells.All(snapshot.KnownSpells.Contains)
            || !snapshot.KnownSpells.All(spell => definition.StartingSpells.Contains(spell) || taught.Contains(spell)))
        {
            throw new ArgumentException($"Creature {snapshot.Id} knows spells its starting kit and its packages do not give it, or lacks a starting one.", nameof(snapshot));
        }

        // The rules that price an action read the snapshot's derived values; the rules that apply it read the
        // restored creature's, recomputed from its conditions. A snapshot where the two disagree would be
        // resolved on one board and applied to another.
        var creature = new Creature(snapshot, definition);
        if (creature.MaxHealth != snapshot.MaxHealth
            || creature.TotalDefense != snapshot.TotalDefense
            || creature.CurrentInitiative != snapshot.CurrentInitiative
            || creature.CriticalChance != snapshot.CriticalChance
            || creature.IsStunned != snapshot.IsStunned)
        {
            throw new ArgumentException($"Creature {snapshot.Id}'s snapshot disagrees with the conditions it carries.", nameof(snapshot));
        }

        // The immunity starts only when a stun ends and lasts one round (ADR 0072): no match leaves a creature
        // stunned and immune at once, immune while dead, or immune for longer than that.
        if (snapshot.StunImmunityRounds is < 0 or > StunImmunityDuration
            || (snapshot.StunImmunityRounds > 0 && (snapshot.IsStunned || snapshot.IsDead)))
        {
            throw new ArgumentException($"Creature {snapshot.Id}'s snapshot carries a stun immunity no match leaves behind.", nameof(snapshot));
        }

        return creature;
    }

    public bool KnowsSpell(SpellId spellId) => _knownSpells.Contains(spellId);

    /// <summary>
    /// Whether this creature bought a package. Asked of the tier and never of its spells: a creature can know
    /// every spell of a package without having bought it, and the prerequisite rule is about the purchase.
    /// </summary>
    public bool OwnsTier(TierId tierId) => _acquiredTiers.Contains(tierId);

    /// <summary>
    /// Buys a package: every spell it teaches at once, and its initiative bonus exactly once, for the rest of
    /// the match.
    /// <para>
    /// Everything is checked before anything changes, so a refused purchase leaves the creature as it was --
    /// no half-taught package, and no bonus without the tier that paid for it. A spell the creature already
    /// knows is granted idempotently rather than refused, because the package is what is being bought and
    /// two packages may legitimately teach the same spell; owning the <em>tier</em> is what blocks a repeat.
    /// </para>
    /// </summary>
    internal Result BuyTier(Tier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);

        if (IsDead)
        {
            return Result.Failure(CreatureErrors.Dead);
        }

        if (_acquiredTiers.Contains(tier.Id))
        {
            return Result.Failure(CreatureErrors.TierAlreadyOwned);
        }

        if (!tier.Prerequisites.All(_acquiredTiers.Contains))
        {
            return Result.Failure(CreatureErrors.TierPrerequisiteMissing);
        }

        _acquiredTiers.Add(tier.Id);
        foreach (var spell in tier.Spells)
        {
            Learn(spell);
        }

        BaseInitiative = BaseInitiative.Plus(tier.InitiativeBonus.Value);
        return Result.Success();
    }

    /// <summary>
    /// Adds a spell to what this creature knows, idempotently. It grants no initiative of its own: a bonus
    /// belongs to the package that teaches the spell and is paid once, when the package is bought (ADR 0056).
    /// Idempotent because two packages may legitimately teach the same spell.
    /// </summary>
    internal void Learn(SpellId spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        _knownSpells.Add(spell);
    }

    /// <summary>
    /// Removes health already reduced by defense. Returns the damage actually dealt; a dead creature takes none.
    /// </summary>
    internal int TakeDamage(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (IsDead)
        {
            return 0;
        }

        var dealt = Math.Min(amount, Health.Value);
        Health = Health.Minus(amount);
        return dealt;
    }

    /// <summary>
    /// Gives health back up to the maximum. Returns the amount actually healed; a dead creature cannot be healed.
    /// </summary>
    internal int Heal(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (IsDead)
        {
            return 0;
        }

        var healed = Math.Min(amount, MaxHealth.Value - Health.Value);
        Health = Health.Plus(healed);
        return healed;
    }

    /// <summary>
    /// Returns the energy actually gained; a dead creature gains none.
    /// </summary>
    internal int GainEnergy(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (IsDead)
        {
            return 0;
        }

        Energy = Energy.Plus(amount);
        return amount;
    }

    /// <summary>
    /// Returns the energy actually taken, which is at most what the creature had; a dead creature loses none.
    /// </summary>
    internal int LoseEnergy(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (IsDead)
        {
            return 0;
        }

        var taken = Math.Min(amount, Energy.Value);
        Energy = Energy.Minus(taken);
        return taken;
    }

    internal Result SpendEnergy(Energy cost)
    {
        ArgumentNullException.ThrowIfNull(cost);

        if (IsDead)
        {
            return Result.Failure(CreatureErrors.Dead);
        }

        if (Energy < cost)
        {
            return Result.Failure(CreatureErrors.NotEnoughEnergy);
        }

        Energy = Energy.Minus(cost.Value);
        return Result.Success();
    }

    /// <summary>
    /// Attaches a lasting effect per its stacking policy. Returns the resulting condition, or <c>null</c> when the
    /// application was ignored or the creature is dead.
    /// </summary>
    internal Condition? Apply(LastingEffect effect, ConditionSource? source = null)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (IsDead || (effect is Stun && !CanBeStunned))
        {
            return null;
        }

        return _conditions.Apply(effect, source);
    }

    /// <summary>
    /// A stun lands only on a creature that is neither stunned nor immune (ADR 0072): a second stun does not
    /// restart the first, so every stun ends, and the round after it ends the creature acts.
    /// </summary>
    internal bool CanBeStunned => IsAlive && !IsStunned && !IsStunImmune;

    /// <summary>
    /// Counts one round down on every condition and returns the ones that expired. The rules decide when in the
    /// round this happens.
    /// </summary>
    internal IReadOnlyList<Condition> TickConditions()
    {
        // The immunity counts down with the conditions, before a stun ending this cleanup can start a new one:
        // a stun that ends here makes the creature immune through the next round, and not a round longer.
        if (_stunImmunity > 0)
        {
            _stunImmunity--;
        }

        var expired = _conditions.Tick();
        if (IsAlive && expired.Any(condition => condition.Effect is Stun))
        {
            _stunImmunity = StunImmunityDuration;
        }

        return expired;
    }

    public CreatureSnapshot Snapshot() => new()
    {
        Id = Id,
        Owner = Owner,
        DefinitionId = Definition.Id,
        Name = Name,
        TalentTree = Definition.TalentTree,
        Health = Health,
        MaxHealth = MaxHealth,
        Energy = Energy,
        TotalDefense = TotalDefense,
        BaseInitiative = BaseInitiative,
        CurrentInitiative = CurrentInitiative,
        CriticalChance = CriticalChance,
        IsStunned = IsStunned,
        StunImmunityRounds = IsAlive ? _stunImmunity : 0,
        KnownSpells = _knownSpells.ToHashSet(),
        AcquiredTiers = _acquiredTiers.ToHashSet(),
        Conditions = [.. _conditions.Active.Select(condition => condition.Snapshot())],
    };
}

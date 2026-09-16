using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
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
    private readonly ConditionSet _conditions;

    private Creature(CreatureId id, PlayerSlot owner, CreatureDefinition definition)
        : base(id)
    {
        Owner = owner;
        Definition = definition;
        Health = definition.BaseStats.Health;
        Energy = definition.BaseStats.Energy;
        BaseInitiative = definition.BaseStats.Initiative;
        _knownSpells = [.. definition.StartingSpells];
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
        _conditions = new ConditionSet(snapshot.Conditions);
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

    public IReadOnlySet<SpellId> KnownSpells => _knownSpells;

    public IReadOnlyList<Condition> Conditions => _conditions.Active;

    public bool IsDead => Health.IsZero;

    public bool IsAlive => !IsDead;

    public bool IsStunned => IsAlive && _conditions.Has<Stun>();

    public Defense TotalDefense => BaseStats.Defense
        .Plus(_conditions.Sum<DefenseBuff>(buff => buff.Amount))
        .Minus(_conditions.Sum<DefenseDebuff>(debuff => debuff.Amount));

    /// <summary>
    /// The creature's own initiative, before any condition: the definition's, raised for good by the Spell
    /// initiative of every spell it has unlocked in this match (ADR 0017). Starting spells are part of the
    /// definition's block and do not raise it.
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
    /// is a copy of a real creature, so its health cannot exceed the definition's; a caller that hands one
    /// where it does has a bug, not a rule violation.
    /// </summary>
    internal static Creature Restore(CreatureSnapshot snapshot, CreatureDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Id != snapshot.DefinitionId)
        {
            throw new ArgumentException($"Creature {snapshot.Id} was spawned from '{snapshot.DefinitionId.Value}', not '{definition.Id.Value}'.", nameof(definition));
        }

        if (snapshot.Health > definition.BaseStats.Health)
        {
            throw new ArgumentException($"Creature {snapshot.Id} cannot have {snapshot.Health} health out of {definition.BaseStats.Health}.", nameof(snapshot));
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

        return creature;
    }

    public bool KnowsSpell(SpellId spellId) => _knownSpells.Contains(spellId);

    /// <summary>
    /// Learns a spell and raises the base initiative by its Spell initiative, for the rest of the match. A
    /// refused unlock raises nothing: a creature that already knows the spell, or is dead, keeps its base.
    /// </summary>
    internal Result UnlockSpell(Spell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);

        if (IsDead)
        {
            return Result.Failure(CreatureErrors.Dead);
        }

        if (!_knownSpells.Add(spell.Id))
        {
            return Result.Failure(CreatureErrors.SpellAlreadyKnown);
        }

        BaseInitiative = BaseInitiative.Plus(spell.Stats.SpellInitiative.Value);
        return Result.Success();
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
        return IsDead ? null : _conditions.Apply(effect, source);
    }

    /// <summary>
    /// Counts one round down on every condition and returns the ones that expired. The rules decide when in the
    /// round this happens.
    /// </summary>
    internal IReadOnlyList<Condition> TickConditions() => _conditions.Tick();

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
        KnownSpells = _knownSpells.ToHashSet(),
        Conditions = [.. _conditions.Active.Select(condition => condition.Snapshot())],
    };
}

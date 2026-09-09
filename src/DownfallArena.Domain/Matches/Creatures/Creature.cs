using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Matches.Creatures;

/// <summary>
/// A combat unit in a match, spawned from a creature definition. Every state change goes through a method that
/// protects the invariants; derived values (stun, total defense, current initiative) come from the conditions.
/// The mutators are internal: only the <see cref="Match"/> aggregate and the rules it runs may change a creature.
/// </summary>
public sealed class Creature : Entity<CreatureId>
{
    private readonly HashSet<SpellId> _knownSpells;
    private readonly ConditionSet _conditions = new();

    private Creature(CreatureId id, PlayerSlot owner, CreatureDefinition definition)
        : base(id)
    {
        Owner = owner;
        Definition = definition;
        Health = definition.BaseStats.Health;
        Energy = definition.BaseStats.Energy;
        BaseInitiative = definition.BaseStats.Initiative;
        _knownSpells = [.. definition.StartingSpells];
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

    public Defense TotalDefense => BaseStats.Defense.Plus(_conditions.Sum<DefenseBuff>(buff => buff.Amount));

    /// <summary>
    /// The creature's own initiative, before any condition: the definition's, raised for good by the Spell
    /// initiative of every spell it has unlocked in this match (ADR 0017). Starting spells are part of the
    /// definition's block and do not raise it.
    /// </summary>
    public Initiative BaseInitiative { get; private set; }

    /// <summary>The base initiative less the active initiative debuffs, which is what orders the timeline.</summary>
    public Initiative CurrentInitiative => BaseInitiative.Minus(_conditions.Sum<InitiativeDebuff>(debuff => debuff.Amount));

    public CriticalChance CriticalChance => BaseStats.CriticalChance;

    public static Creature Spawn(CreatureId id, PlayerSlot owner, CreatureDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new Creature(id, owner, definition);
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
    /// Restores health up to the maximum. Returns the amount actually healed; a dead creature cannot be healed.
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
    internal Condition? Apply(LastingEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return IsDead ? null : _conditions.Apply(effect);
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

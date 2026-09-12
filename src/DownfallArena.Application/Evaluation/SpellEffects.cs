namespace DownfallArena.Application.Evaluation;

/// <summary>
/// What one spell actually did, as opposed to how often it was chosen: how many of its declarations turned into
/// a cast rather than fizzling, and what those casts added up to.
/// <para>
/// This is what a cost change moves and what counting declarations cannot see. Raise a spell's cost and a
/// creature still declares it — the declaration is a choice made before the energy is checked — but it fizzles
/// far more often, casts less, and does less. The sides that declared it do not budge.
/// </para>
/// <para>
/// Every total is what the board took rather than what the rules computed, so a hit that overkills counts the
/// health it actually removed. <see cref="Damage"/> is the damage a cast dealt on the spot;
/// <see cref="ConditionDamage"/> is what its bleeds went on to take at upkeep, rounds later, which a condition
/// now remembers its cast well enough to be counted for (ADR 0027). The two are kept apart because they are
/// different questions -- how hard a cast hits, and what it is worth in total -- and adding them is the
/// caller's to do.
/// </para>
/// </summary>
public sealed record SpellEffects(
    int Resolved,
    int Fizzled,
    int Criticals,
    int Damage,
    int Healing,
    int Energy,
    int Stuns,
    int Bleeds,
    int Regens,
    int EnergyRegenerations,
    int DefenseBuffs,
    int InitiativeDebuffs,
    int ConditionDamage = 0,
    int ConditionHealing = 0,
    int ConditionEnergy = 0)
{
    public static SpellEffects None { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Everything a spell's casts took off an enemy, on the spot and at upkeep (ADR 0027).</summary>
    public int TotalDamage => Damage + ConditionDamage;

    /// <summary>Declarations that reached resolution, whether they landed or fizzled.</summary>
    public int Casts => Resolved + Fizzled;

    /// <summary>The share of those that landed. A spell nobody can afford falls towards zero.</summary>
    public double ResolveRate => Casts == 0 ? 0 : (double)Resolved / Casts;

    public SpellEffects Plus(SpellEffects other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new SpellEffects(
            Resolved + other.Resolved,
            Fizzled + other.Fizzled,
            Criticals + other.Criticals,
            Damage + other.Damage,
            Healing + other.Healing,
            Energy + other.Energy,
            Stuns + other.Stuns,
            Bleeds + other.Bleeds,
            Regens + other.Regens,
            EnergyRegenerations + other.EnergyRegenerations,
            DefenseBuffs + other.DefenseBuffs,
            InitiativeDebuffs + other.InitiativeDebuffs,
            ConditionDamage + other.ConditionDamage,
            ConditionHealing + other.ConditionHealing,
            ConditionEnergy + other.ConditionEnergy);
    }
}

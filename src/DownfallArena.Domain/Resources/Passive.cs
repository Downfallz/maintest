namespace DownfallArena.Domain.Resources;

/// <summary>
/// What a Tier gives its owner for as long as the owner holds it, beside the spells it teaches and its initiative
/// bonus (ADR 0101): a closed set of standing properties the rules name, read from the packages a creature owns
/// rather than stored on it.
/// </summary>
public sealed record Passive
{
    private Passive(bool stunImmunity, int upkeepEnergy, int damageBonus, int sunder)
    {
        StunImmunity = stunImmunity;
        UpkeepEnergy = upkeepEnergy;
        DamageBonus = damageBonus;
        Sunder = sunder;
    }

    /// <summary>The owner cannot be stunned.</summary>
    public bool StunImmunity { get; }

    /// <summary>Energy the owner gains at every upkeep, beside the rule set's own.</summary>
    public int UpkeepEnergy { get; }

    /// <summary>Added to every direct hit the owner deals, as a damage buff held for good would be.</summary>
    public int DamageBonus { get; }

    /// <summary>
    /// Taken off the target's total defense by every direct hit the owner deals, never below zero (ADR 0106): a
    /// hit that ignores some of the armour, which is worth nothing against a target that has none.
    /// </summary>
    public int Sunder { get; }

    /// <summary>A package that gives nothing beyond its spells and its initiative bonus: every package before ADR 0101.</summary>
    public static Passive None { get; } = new(false, 0, 0, 0);

    /// <summary>A passive, refusing a negative amount: a package that took energy or damage away would be a curse, not a purchase.</summary>
    public static Passive Of(bool stunImmunity = false, int upkeepEnergy = 0, int damageBonus = 0, int sunder = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(upkeepEnergy);
        ArgumentOutOfRangeException.ThrowIfNegative(damageBonus);
        ArgumentOutOfRangeException.ThrowIfNegative(sunder);
        return new Passive(stunImmunity, upkeepEnergy, damageBonus, sunder);
    }

    /// <summary>What a creature owning every one of <paramref name="tiers"/> holds.</summary>
    public static Passive Of(IEnumerable<Tier> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);
        return tiers.Aggregate(None, (held, tier) => held.With(tier.Passive));
    }

    /// <summary>Whether the package gives anything at all through it.</summary>
    public bool GivesAnything() => StunImmunity || UpkeepEnergy > 0 || DamageBonus > 0 || Sunder > 0;

    /// <summary>What a creature owning both holds: an immunity once, amounts added.</summary>
    public Passive With(Passive other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Passive(StunImmunity || other.StunImmunity, UpkeepEnergy + other.UpkeepEnergy, DamageBonus + other.DamageBonus, Sunder + other.Sunder);
    }
}

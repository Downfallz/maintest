namespace DownfallArena.Domain.Resources;

/// <summary>
/// What a Tier gives its owner for as long as the owner holds it, beside the spells it teaches and its initiative
/// bonus (ADR 0100): a closed set of standing properties the rules name, read from the packages a creature owns
/// rather than stored on it.
/// </summary>
/// <param name="StunImmunity">The owner cannot be stunned.</param>
/// <param name="UpkeepEnergy">Energy the owner gains at every upkeep, beside the rule set's own.</param>
/// <param name="DamageBonus">Added to every direct hit the owner deals, as a damage buff held for good would be.</param>
public sealed record Passive(bool StunImmunity, int UpkeepEnergy, int DamageBonus)
{
    /// <summary>A package that gives nothing beyond its spells and its initiative bonus: every package before ADR 0100.</summary>
    public static Passive None { get; } = new(false, 0, 0);

    /// <summary>A passive, refusing a negative amount: a package that took energy or damage away would be a curse, not a purchase.</summary>
    public static Passive Of(bool stunImmunity = false, int upkeepEnergy = 0, int damageBonus = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(upkeepEnergy);
        ArgumentOutOfRangeException.ThrowIfNegative(damageBonus);
        return new Passive(stunImmunity, upkeepEnergy, damageBonus);
    }

    /// <summary>Whether the package gives anything at all through it.</summary>
    public bool GivesAnything() => StunImmunity || UpkeepEnergy > 0 || DamageBonus > 0;

    /// <summary>What a creature owning both holds: an immunity once, amounts added.</summary>
    public Passive With(Passive other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Passive(StunImmunity || other.StunImmunity, UpkeepEnergy + other.UpkeepEnergy, DamageBonus + other.DamageBonus);
    }

    /// <summary>What a creature owning every one of <paramref name="tiers"/> holds.</summary>
    public static Passive Of(IEnumerable<Tier> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);
        return tiers.Aggregate(None, (held, tier) => held.With(tier.Passive));
    }
}

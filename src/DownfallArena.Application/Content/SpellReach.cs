namespace DownfallArena.Application.Content;

/// <summary>
/// What one spell costs and does, next to how far it reaches: how many creature definitions start with it, and
/// how many could ever come to know it. The numbers are the authored ones, before criticals, defense and the
/// board — the point is to compare spells with each other, not to predict a match.
/// </summary>
public sealed record SpellReach
{
    public required string Spell { get; init; }

    public required string Name { get; init; }

    public required string CreatureClass { get; init; }

    public required int Cost { get; init; }

    public required int Initiative { get; init; }

    /// <summary>Direct damage to one target.</summary>
    public required int Damage { get; init; }

    /// <summary>
    /// Damage over time to one target, over the rounds the condition lasts or the round cap when it outlasts the
    /// match. Content can author a bleed longer than a match, and those rounds never happen.
    /// </summary>
    public required int BleedDamage { get; init; }

    public required int Healing { get; init; }

    /// <summary>Targets the spell takes, or <c>null</c> when it takes every legal target.</summary>
    public int? MaxTargets { get; init; }

    /// <summary>Creature definitions that know it from the first round.</summary>
    public required int StartingFor { get; init; }

    /// <summary>Creature definitions that can ever know it: starting spells and everything the talents unlock.</summary>
    public required int ReachableBy { get; init; }

    /// <summary>
    /// Damage per point of energy, the crude ratio to sort a cost change by. A spell that costs nothing counts
    /// its damage whole rather than dividing by zero.
    /// </summary>
    public double DamagePerEnergy => Cost == 0 ? Damage + BleedDamage : (Damage + BleedDamage) / (double)Cost;
}

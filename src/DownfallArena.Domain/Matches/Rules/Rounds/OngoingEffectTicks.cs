namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>
/// What the start of a round did to the creatures before anyone acts, in the order it was applied: the energy
/// its energy regenerations gave, the healing its regenerations gave and the damage its bleeds took. Healing is
/// applied before the bleeds (ADR 0019); energy goes first, which is what lets a creature its own bleed kills
/// keep what it gained (ADR 0020).
/// </summary>
public sealed record OngoingEffectTicks(
    IReadOnlyList<EnergyRegenerationTick> EnergyRegenerationTicks,
    IReadOnlyList<RegenerationTick> RegenerationTicks,
    IReadOnlyList<BleedTick> BleedTicks)
{
    public static OngoingEffectTicks None { get; } = new([], [], []);
}

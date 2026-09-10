namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>
/// What the start of a round did to the creatures before anyone acts: the energy its attunements gave, the
/// healing its regenerations gave and the damage its bleeds took. Healing is applied before the bleeds
/// (ADR 0019); energy touches no health, so where it falls among them cannot change an outcome.
/// </summary>
public sealed record OngoingEffectTicks(
    IReadOnlyList<BleedTick> BleedTicks,
    IReadOnlyList<RegenerationTick> RegenerationTicks,
    IReadOnlyList<AttunementTick> AttunementTicks)
{
    public static OngoingEffectTicks None { get; } = new([], [], []);
}

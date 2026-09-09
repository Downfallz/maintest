namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>
/// What the start of a round did to the creatures before anyone acts: the healing its regenerations gave and
/// the damage its bleeds took. Healing is applied first (ADR 0019).
/// </summary>
public sealed record OngoingEffectTicks(IReadOnlyList<BleedTick> BleedTicks, IReadOnlyList<RegenerationTick> RegenerationTicks)
{
    public static OngoingEffectTicks None { get; } = new([], []);
}

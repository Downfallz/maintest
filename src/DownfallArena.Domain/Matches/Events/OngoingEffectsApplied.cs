using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The start of the round was applied, in the order it happened: the energy regenerations gave their energy,
/// the regenerations healed, then the bleeds dealt their damage.
/// </summary>
public sealed record OngoingEffectsApplied(
    MatchId MatchId,
    RoundId RoundId,
    IReadOnlyList<EnergyRegenerationTick> EnergyRegenerationTicks,
    IReadOnlyList<RegenerationTick> RegenerationTicks,
    IReadOnlyList<BleedTick> BleedTicks) : IMatchEvent;

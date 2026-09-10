using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The start of the round was applied: the attunements gave their energy, the regenerations healed, then the
/// bleeds dealt their damage.
/// </summary>
public sealed record OngoingEffectsApplied(
    MatchId MatchId,
    RoundId RoundId,
    IReadOnlyList<BleedTick> BleedTicks,
    IReadOnlyList<RegenerationTick> RegenerationTicks,
    IReadOnlyList<AttunementTick> AttunementTicks) : IMatchEvent;

using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The start of the round was applied: the regenerations healed, then the bleeds dealt their damage.
/// </summary>
public sealed record OngoingEffectsApplied(MatchId MatchId, RoundId RoundId, IReadOnlyList<BleedTick> BleedTicks, IReadOnlyList<RegenerationTick> RegenerationTicks) : IMatchEvent;

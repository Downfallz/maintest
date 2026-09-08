using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The bleed ticks of the start of the round were dealt.
/// </summary>
public sealed record OngoingEffectsApplied(MatchId MatchId, RoundId RoundId, IReadOnlyList<BleedTick> BleedTicks) : IMatchEvent;

using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The match ended in the given round, with its outcome (ADR 0011, ADR 0083).
/// </summary>
public sealed record MatchEnded(MatchId MatchId, RoundId RoundId, MatchOutcome Outcome) : IMatchEvent;

using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The round is over: finalized, with the round cap checked right after, or cut short by a team wiped on an
/// action or at upkeep (ADR 0083), in which case the match ends with it.
/// </summary>
public sealed record RoundEnded(MatchId MatchId, RoundId RoundId) : IMatchEvent;

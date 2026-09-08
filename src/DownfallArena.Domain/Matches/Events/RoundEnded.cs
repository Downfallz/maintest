using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The round was finalized; the win condition was checked right after.
/// </summary>
public sealed record RoundEnded(MatchId MatchId, RoundId RoundId) : IDomainEvent;

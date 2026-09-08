using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A new round began, in its first sub-phase.
/// </summary>
public sealed record RoundStarted(MatchId MatchId, RoundId RoundId) : IDomainEvent;

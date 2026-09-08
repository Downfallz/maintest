using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The match ended after the given round, with its outcome (ADR 0011).
/// </summary>
public sealed record MatchEnded(MatchId MatchId, RoundId LastRound, MatchOutcome Outcome) : IDomainEvent;

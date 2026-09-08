using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// Both players joined; the first round begins. The content hash pins the game resources the match plays with (ADR 0009).
/// </summary>
public sealed record MatchStarted(MatchId MatchId, PlayerId Player1, PlayerId Player2, string ContentHash) : IDomainEvent;

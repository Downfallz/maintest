using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// Both players joined; the first round begins.
/// </summary>
public sealed record MatchStarted(MatchId MatchId, PlayerId Player1, PlayerId Player2) : IDomainEvent;

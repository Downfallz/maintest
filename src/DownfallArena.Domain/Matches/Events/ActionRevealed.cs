using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The next intent of the timeline was revealed and bound to its targets.
/// </summary>
public sealed record ActionRevealed(MatchId MatchId, RoundId RoundId, CombatAction Action) : IDomainEvent;

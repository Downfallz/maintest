using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A combat action resolved and its outcomes were applied to the creatures.
/// </summary>
public sealed record CombatActionResolved(MatchId MatchId, RoundId RoundId, CombatResolution Resolution) : IDomainEvent;

using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The cleanup of the round counted every condition down; these are the ones that expired, per creature.
/// </summary>
public sealed record ConditionsExpired(MatchId MatchId, RoundId RoundId, IReadOnlyDictionary<CreatureId, IReadOnlyList<ConditionSnapshot>> Expired) : IDomainEvent;

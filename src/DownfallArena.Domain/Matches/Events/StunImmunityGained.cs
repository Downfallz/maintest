using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The cleanup of the round ended these creatures' stuns, and each is immune to stun through the next round
/// (ADR 0072). Raised after <see cref="ConditionsExpired"/>, which carries the stuns that ended.
/// </summary>
public sealed record StunImmunityGained(MatchId MatchId, RoundId RoundId, IReadOnlyList<CreatureId> Creatures) : IMatchEvent;

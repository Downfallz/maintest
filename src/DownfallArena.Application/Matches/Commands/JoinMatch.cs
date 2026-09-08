using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Commands;

/// <summary>
/// Seats a player with their roster. The match starts when the second player joins.
/// </summary>
public sealed record JoinMatch(MatchId MatchId, PlayerId Player, IReadOnlyList<CreatureDefinitionId> Roster);

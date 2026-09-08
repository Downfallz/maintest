using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches;

/// <summary>
/// What resolving one combat action did: the resolution, and whether it completed the round or the match.
/// The round id is the one the action belonged to, even when the next round has already started.
/// </summary>
public sealed record CombatStep(RoundId Round, CombatResolution Resolution, bool RoundCompleted, bool MatchEnded);

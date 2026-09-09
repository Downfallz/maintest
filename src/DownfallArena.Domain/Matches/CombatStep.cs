using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches;

/// <summary>
/// What resolving one combat action did: what the rules computed, what the board took, and whether it
/// completed the round or the match. The round id is the one the action belonged to, even when the next round
/// has already started.
/// </summary>
/// <param name="Resolution">What the action aimed for.</param>
/// <param name="AppliedOutcomes">What it actually did, which is smaller when damage overkills, healing overheals, or a condition is refused.</param>
public sealed record CombatStep(
    RoundId RoundId,
    CombatResolution Resolution,
    IReadOnlyList<EffectOutcome> AppliedOutcomes,
    bool RoundCompleted,
    bool MatchCompleted);

using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A combat action resolved and its outcomes were applied to the creatures.
/// <para>
/// <see cref="Resolution"/> holds what the rules computed; <see cref="AppliedOutcomes"/> what the board took,
/// which is smaller whenever damage overkills, healing overheals, or a condition is refused. Anything counting
/// what a spell did reads the applied outcomes; anything explaining what was attempted reads the resolution.
/// </para>
/// </summary>
public sealed record CombatActionResolved(
    MatchId MatchId,
    RoundId RoundId,
    CombatResolution Resolution,
    IReadOnlyList<EffectOutcome> AppliedOutcomes) : IMatchEvent;

using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rules.Combat;

namespace DownfallArena.Domain.Matches.Rules;

/// <summary>
/// What <see cref="Advance.Action"/> answers: the resolution the rules computed, the outcomes the board took,
/// and the board after them. The same three things a <see cref="CombatStep"/> reports, without the round
/// position, because no round was played.
/// </summary>
/// <param name="Resolution">What the action aimed for.</param>
/// <param name="AppliedOutcomes">What it actually did, smaller when damage overkills, healing overheals, or a condition is refused.</param>
/// <param name="Board">Every creature after the action, in the order they were handed in.</param>
public sealed record AdvancedAction(
    CombatResolution Resolution,
    IReadOnlyList<EffectOutcome> AppliedOutcomes,
    IReadOnlyList<CreatureSnapshot> Board);

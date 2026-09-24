using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// Public state immediately around one combat action, before cleanup or the next upkeep can run.
/// No private planning choices are retained here (ADR 0075).
/// </summary>
public sealed record CombatActionFrame(
    IReadOnlyList<CreatureSnapshot> Before,
    IReadOnlyList<CreatureSnapshot> After,
    IReadOnlyList<ActivationSlot> Timeline,
    IReadOnlyList<RollOff> RollOffs);

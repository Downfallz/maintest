using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// What one creature rolled in a Roll-off (ADR 0063): its d20 rolls in the order they were made, the first
/// roll and then any roll again after a number the other side matched.
/// </summary>
public sealed record RollOff(CreatureId Creature, IReadOnlyList<int> Rolls);

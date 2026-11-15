using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// What a Lesson step points at (ADR 0187): a creature on the board, or a control of the page by the name the
/// coach knows it under. One or the other, never both.
/// </summary>
/// <param name="Creature">The creature pointed at, an ally or an enemy.</param>
/// <param name="Control">The control pointed at, by its name on the page.</param>
public sealed record LessonPointer(CreatureId? Creature, string? Control);

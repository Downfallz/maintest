using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Resources.Talents;

/// <summary>
/// A spell offered by a talent node, with its own prerequisites.
/// </summary>
public sealed record TalentSpell(SpellId Id, TalentPrerequisites Prerequisites);

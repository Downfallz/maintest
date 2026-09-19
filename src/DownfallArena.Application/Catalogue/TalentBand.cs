using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Catalogue;

/// <summary>
/// One node of a talent tree as a band of cards: where it sits, how deep it is, and what is unlocked from it.
/// </summary>
/// <param name="Depth">1 for the root, one more for each step away from it — the band the mat draws.</param>
/// <param name="ParentCode">The parent node in this tree, or null for the root. Class ancestry is distinct from spell prerequisites.</param>
public sealed record TalentBand(TalentTreeId Tree, string TreeName, string Code, string Name, int Depth, IReadOnlyList<SpellId> Spells, string? ParentCode = null);

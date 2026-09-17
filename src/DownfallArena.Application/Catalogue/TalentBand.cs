using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Catalogue;

/// <summary>
/// One node of a talent tree as a band of cards: where a card sits, and what is unlocked from it.
/// </summary>
public sealed record TalentBand(TalentTreeId Tree, string TreeName, string Code, string Name, IReadOnlyList<SpellId> Spells);

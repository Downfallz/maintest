using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Catalogue;

/// <summary>
/// One package a pick buys, as the card a player is shown before spending one: its name, how deep it sits,
/// what it needs first, everything it teaches, and the initiative it is worth (ADR 0056).
/// </summary>
/// <remarks>
/// A client is handed the whole card rather than an id, because a package is chosen on what it contains and a
/// screen that had to look its spells up one by one would be a second place that decides what a package is.
/// </remarks>
public sealed record PackageCard(
    TierId Id,
    string Name,
    int Level,
    IReadOnlyList<TierId> Prerequisites,
    IReadOnlyList<SpellId> Spells,
    int InitiativeBonus);

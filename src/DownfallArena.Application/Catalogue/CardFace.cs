using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Catalogue;

/// <summary>
/// One spell as a card: everything needed to resolve a cast, in the words the printed deck uses
/// (<c>docs/tabletop/components.md</c> §2.1).
/// </summary>
/// <param name="Id">The versioned spell id, which is also what a decision names on the wire.</param>
/// <param name="Cost">Energy, spent at the intent.</param>
/// <param name="Initiative">What the creature's base initiative gains once, at the unlock (ADR 0017).</param>
/// <param name="Targeting">Origin, scope and count as one sentence: <c>Self</c>, <c>One enemy</c>, <c>Up to 2 allies</c>.</param>
/// <param name="CasterEffects">What the cast does to whoever cast it, once per cast (ADR 0031).</param>
/// <param name="Critical">The chance as a percentage, always printed.</param>
/// <param name="CriticalThreshold">
/// The face of a d20 this spell crits on, or <c>null</c> when the chance is not a twentieth and the card has to
/// fall back to the percentage (<c>docs/tabletop/components.md</c>:778). Zero is <c>null</c>: a spell that never
/// crits has no face to roll.
/// </param>
/// <param name="Tree">The talent tree the spell is unlocked from, and the node within it.</param>
/// <param name="Requires">The spells a creature must already know, or <c>null</c> when nothing gates it.</param>
public sealed record CardFace(
    SpellId Id,
    string Name,
    SpellType Type,
    CreatureClass CreatureClass,
    int Cost,
    int Initiative,
    string Targeting,
    IReadOnlyList<string> Effects,
    IReadOnlyList<string> CasterEffects,
    string Critical,
    int? CriticalThreshold,
    string? Tree,
    string? Tier,
    string? Requires);

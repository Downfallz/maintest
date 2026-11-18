namespace DownfallArena.Application.Catalogue;

/// <summary>
/// How a confirmed cast of the spell is drawn (ADR 0131): the family that colours it, the motif that shapes it and
/// the delivery that moves the caster or not, and, where the spell has one, its staging: the beats the effects kit
/// plays (ADR 0196). Presentation authored beside the content, never a rule.
/// </summary>
public sealed record SpellVisual(string Family, string Motif, string Delivery, IReadOnlyList<SpellBeat>? Staging = null);

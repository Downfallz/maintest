namespace DownfallArena.Application.Catalogue;

/// <summary>
/// One beat of a spell's staging (ADR 0196): what the battle's effects kit plays, when it starts in the action,
/// in milliseconds at 1×, and its parameters. A parameter left out is the kit's own default. Presentation
/// authored beside the content, never a rule.
/// </summary>
/// <param name="Beat">The beat's kind: the motif itself, or one of the kit's primitives.</param>
/// <param name="At">When it starts, in milliseconds from the start of the action, at 1×.</param>
/// <param name="On">Where it plays: on the caster or on each target.</param>
/// <param name="Lasts">How long it lasts, in milliseconds at 1×.</param>
/// <param name="Count">How many particles it holds.</param>
/// <param name="Shape">The particle's shape, a frame of the kit's texture.</param>
/// <param name="Tint">The colour, by its name in the palette.</param>
/// <param name="Blend">How it is blended over the field.</param>
/// <param name="Direction">Where it heads, relative to the blow.</param>
/// <param name="Spread">The width of its fan, in degrees.</param>
/// <param name="Reach">How far it reaches, as a percentage of the figure's radius.</param>
/// <param name="Strength">How strong it is, between 0 and 1.</param>
/// <param name="Level">Where on its figure it starts: its feet, its centre or its head.</param>
public sealed record SpellBeat(
    string Beat,
    int At,
    string? On = null,
    int? Lasts = null,
    int? Count = null,
    string? Shape = null,
    string? Tint = null,
    string? Blend = null,
    string? Direction = null,
    int? Spread = null,
    int? Reach = null,
    double? Strength = null,
    string? Level = null);

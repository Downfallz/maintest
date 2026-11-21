namespace DownfallArena.Application.Catalogue;

/// <summary>
/// The kind of one effect a card prints, in the order it prints them: the card's <c>Effects</c> lines, then its
/// <c>CasterEffects</c> lines. A screen marks the line with the catalogue's <see cref="EffectCue" /> for that kind
/// (the stat it moves, and whether it is a loss) and never has to read the words.
/// </summary>
/// <param name="Kind">The effect's name on the wire, as <see cref="EffectCue.Kind" /> names it.</param>
/// <param name="OnCaster">Whether the line is one of the caster's.</param>
/// <param name="Figures">
/// The line's numbers alone, for a face too narrow for its words (the spellbook's two columns,
/// <c>docs/art/journeys/windows.md</c>): <c>3</c>, <c>+1 · 3</c> (the amount, then the rounds), <c>+3 · perm.</c>.
/// The words stay the line's; the screen shows these beside the stat's icon and keeps the words for a screen reader.
/// </param>
public sealed record CardMark(string Kind, bool OnCaster, string Figures);

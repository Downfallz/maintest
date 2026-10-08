namespace DownfallArena.Application.Catalogue;

/// <summary>
/// How a screen marks one kind of effect: the glyph of the stat it moves, and whether it is a loss. Served
/// with the catalogue so the page never names an effect (<c>TablePageContentTests</c>): a kind added to the
/// domain gets its mark here, and the page draws it the day it is served.
/// </summary>
/// <param name="Kind">The effect's name on the wire, which is the domain record's.</param>
/// <param name="Glyph">The mark of the stat the effect moves: ♥ health, ϟ energy, ◇ defense, ↟ initiative, ⊘ a stun, ⚔ the damage a holder deals.</param>
/// <param name="Harmful">Whether its number is a loss for whoever carries it, which is the sign a chip prints.</param>
public sealed record EffectCue(string Kind, string Glyph, bool Harmful);

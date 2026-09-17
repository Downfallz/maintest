namespace DownfallArena.Application.Catalogue;

/// <summary>
/// The shape of a round: its sub-phases in the order they are played, and the orderings inside them that a
/// player gets wrong.
/// </summary>
/// <remarks>
/// It is served rather than known by the client for the same reason the cards are: a screen that listed the
/// sub-phases itself would be a screen holding a rule, and this one holds none (ADR 0054). The table prints it
/// as the round strip the printed board carries (<c>docs/tabletop/playtest-app.md</c> §3.1).
/// </remarks>
/// <param name="SubPhases">Every step of a round, in play order.</param>
/// <param name="Orderings">
/// The two the player aid calls load-bearing (<c>docs/tabletop/components.md</c>:645-647), because they are the
/// two a player gets wrong: healing resolves before bleeding, and a critical is applied before defense is
/// subtracted.
/// </param>
public sealed record RoundShape(IReadOnlyList<string> SubPhases, IReadOnlyList<string> Orderings);

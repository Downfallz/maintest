namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// The controls of the page a Lesson step may point at (ADR 0187), by the names the coach knows them under: the
/// spell choice, the cast's confirmation, the command of the round (mutations, tie order), the turn order and the
/// history. The battle view holds its own list to this one (<c>table/battle-tutorial.js</c>).
/// </summary>
public static class LessonControls
{
    public static IReadOnlyList<string> Names { get; } =
        ["spells", "cast", "command", "order", "history"];

    public static bool Has(string name) => Names.Contains(name, StringComparer.Ordinal);
}

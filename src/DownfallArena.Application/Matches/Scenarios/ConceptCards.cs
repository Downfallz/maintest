namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// The Concept cards a Lesson step can open (ADR 0187, docs/tabletop/onboarding-plan.md), by id. Their text is
/// written once, in the official rulebook (`rules/`, ADR 0188), and the table's pages read it from the built book;
/// this is the list a tutorial file is held against, and the book's cards are held to it, so a lesson never names a
/// card nobody wrote.
/// </summary>
public static class ConceptCards
{
    public static IReadOnlyList<string> Ids { get; } =
        ["health", "energy", "defense", "initiative", "tempo", "critical", "intent", "fizzle", "conditions", "mutation"];

    public static bool Has(string id) => Ids.Contains(id, StringComparer.Ordinal);
}

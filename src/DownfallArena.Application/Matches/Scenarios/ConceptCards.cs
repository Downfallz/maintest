namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// The Concept cards a Lesson step can open (ADR 0187, docs/tabletop/onboarding-plan.md), by id. Their text lives
/// in the table's pages, summarised from the rulebook; this is the list a tutorial file is held against, so a
/// lesson never names a card nobody wrote.
/// </summary>
public static class ConceptCards
{
    public static IReadOnlyList<string> Ids { get; } =
        ["health", "energy", "defense", "initiative", "tempo", "critical", "intent", "fizzle", "conditions", "mutation"];

    public static bool Has(string id) => Ids.Contains(id, StringComparer.Ordinal);
}

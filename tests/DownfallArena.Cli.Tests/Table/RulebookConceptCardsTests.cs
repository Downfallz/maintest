using System.Text.Json;
using DownfallArena.Application.Matches.Scenarios;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A concept card is written once, in its section of the official rulebook (ADR 0188, rulebook-plan.md R4), and
/// the table's pages read it from the built book. The ids a lesson step may name (<see cref="ConceptCards.Ids"/>)
/// are the cards the book carries: a card the book lost would open nothing, and a card the list forgot could never
/// be named by a lesson.
/// </summary>
public sealed class RulebookConceptCardsTests
{
    private static readonly string Book = Path.Combine(AppContext.BaseDirectory, "table", "rules.json");

    [Fact]
    public void Every_concept_a_lesson_may_name_has_its_card_in_the_rulebook()
    {
        var cards = CardsInTheBook();

        ConceptCards.Ids.Except(cards, StringComparer.Ordinal).ShouldBeEmpty();
    }

    [Fact]
    public void Every_card_in_the_rulebook_is_a_concept_a_lesson_may_name()
    {
        var cards = CardsInTheBook();

        cards.Where(card => !ConceptCards.Has(card)).ShouldBeEmpty();
    }

    [Fact]
    public void Every_card_the_rulebook_lists_sits_in_one_of_its_sections()
    {
        using var book = JsonDocument.Parse(File.ReadAllText(Book));
        var sectionsWithCards = book.RootElement.GetProperty("parts").EnumerateArray()
            .SelectMany(part => part.GetProperty("sections").EnumerateArray())
            .Where(section => section.TryGetProperty("card", out var card) && card.ValueKind == JsonValueKind.Object)
            .Select(section => section.GetProperty("card").GetProperty("id").GetString())
            .ToList();

        var cards = CardsInTheBook();

        sectionsWithCards.ShouldBe(cards);
    }

    private static List<string> CardsInTheBook()
    {
        using var book = JsonDocument.Parse(File.ReadAllText(Book));
        return [.. book.RootElement.GetProperty("cards").EnumerateArray().Select(card => card.GetProperty("id").GetString()!)];
    }
}

using System.Text.Json;
using DownfallArena.Application.Learning;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// What the host adds to a piece of feedback sent from a seat (docs/tabletop/feedback-plan.md, "What the host
/// attaches"), from what it knows and the page cannot be trusted with. <see cref="Position" /> is the seat's own view,
/// which holds nothing the seat may not see; a practice's entries since its cut are copied, since a practice is kept
/// nowhere. The journal is not copied: its length says where the player stood.
/// </summary>
public sealed record FeedbackContext
{
    /// <summary>The table's session id; none for a practice, which is no table.</summary>
    public string? Table { get; init; }

    /// <summary>The seat, as a URL names it: <c>player1</c> or <c>player2</c>.</summary>
    public string? Seat { get; init; }

    public int? Round { get; init; }

    public string? SubPhase { get; init; }

    /// <summary>
    /// How many entries the table's journal held when the player wrote, decisions or not: the measure a scenario's cut is
    /// taken in, so a reproduced report's answers stop there. A practice's is its own journal's, not its source's.
    /// </summary>
    public int? JournalLength { get; init; }

    /// <summary>The hash of the content the table is played on.</summary>
    public string? Content { get; init; }

    public RuleSetStamp? Rules { get; init; }

    /// <summary>The host's build (ADR 0163).</summary>
    public string? Engine { get; init; }

    /// <summary>The seat's own view when the report was sent, attached by its player; none for an idea.</summary>
    public JsonElement? Position { get; init; }

    /// <summary>The scenario a practice was opened from (ADR 0179).</summary>
    public string? Scenario { get; init; }

    /// <summary>A practice's journal since its cut, one journal line each, its times at the epoch.</summary>
    public IReadOnlyList<JsonElement>? PracticeEntries { get; init; }

    /// <summary>What a second report from the same sender is merged on: the same table, or practice, and round.</summary>
    internal string? Place => Round is { } round && (Table ?? Scenario) is { } where ? $"{where}#{round}" : null;
}

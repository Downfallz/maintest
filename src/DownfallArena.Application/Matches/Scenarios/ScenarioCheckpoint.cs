using System.Text.Json;
using System.Text.Json.Nodes;
using DownfallArena.Application.Matches.Projections;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// Both seats' boards at a scenario's cut, as its source stood there: what a match rebuilt from the scenario is
/// held to before anybody plays it. Kept as text, the boards as JSON with the match's id left out, so a scenario
/// written to a file holds the very thing it is compared on.
/// </summary>
/// <param name="Round">The round the cut is at the top of.</param>
/// <param name="Player1">Player 1's board, as text.</param>
/// <param name="Player2">Player 2's board, as text.</param>
public sealed record ScenarioCheckpoint(int Round, string Player1, string Player2)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The checkpoint of two boards, whatever match each was built under: a scenario is started under an id of its
    /// own, and the boards say nothing else of which match they belong to.
    /// </summary>
    public static ScenarioCheckpoint Of(int round, PlayerBoardState player1, PlayerBoardState player2)
    {
        ArgumentNullException.ThrowIfNull(player1);
        ArgumentNullException.ThrowIfNull(player2);
        return new ScenarioCheckpoint(round, Text(player1), Text(player2));
    }

    /// <summary>
    /// Whether a match rebuilt from the scenario stands where this checkpoint says its source stood: the same round,
    /// and every value this checkpoint recorded found unchanged on the rebuilt boards. A field the boards gained since
    /// the scenario was cut is not part of what was recorded, so a projection that grows does not refuse every
    /// scenario cut before it; a value that changed, or one that is gone, does.
    /// </summary>
    public bool StandsIn(ScenarioCheckpoint rebuilt)
    {
        ArgumentNullException.ThrowIfNull(rebuilt);
        return Round == rebuilt.Round && Within(JsonNode.Parse(Player1), JsonNode.Parse(rebuilt.Player1)) && Within(JsonNode.Parse(Player2), JsonNode.Parse(rebuilt.Player2));
    }

    /// <summary>Whether everything in <paramref name="recorded" /> is in <paramref name="rebuilt" />, as it is.</summary>
    private static bool Within(JsonNode? recorded, JsonNode? rebuilt) => (recorded, rebuilt) switch
    {
        (null, null) => true,
        (JsonObject expected, JsonObject actual) => expected.All(pair => actual.TryGetPropertyValue(pair.Key, out var value) && Within(pair.Value, value)),
        (JsonArray expected, JsonArray actual) => expected.Count == actual.Count && expected.Zip(actual).All(pair => Within(pair.First, pair.Second)),
        (JsonValue expected, JsonValue actual) => JsonNode.DeepEquals(expected, actual),
        _ => false,
    };

    private static string Text(PlayerBoardState board) => JsonSerializer.Serialize(board with { MatchId = default }, Web);
}

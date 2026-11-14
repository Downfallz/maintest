using System.Text.Json;
using DownfallArena.Application.Matches.Projections;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// Both seats' boards at a scenario's cut, as its source stood there: what a match rebuilt from the scenario is
/// held to before anybody plays it.
/// </summary>
public sealed record ScenarioCheckpoint(int Round, PlayerBoardState Player1, PlayerBoardState Player2)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Whether the two stand on the same boards, whatever match each was built under: a scenario is started
    /// under an id of its own, and the boards say nothing else of which match they belong to.
    /// </summary>
    public bool SameBoardsAs(ScenarioCheckpoint other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Round == other.Round && Text(Player1) == Text(other.Player1) && Text(Player2) == Text(other.Player2);
    }

    private static string Text(PlayerBoardState board) => JsonSerializer.Serialize(board with { MatchId = default }, Web);
}

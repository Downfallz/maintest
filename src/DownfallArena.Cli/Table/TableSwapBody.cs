using System.Text.Json.Serialization;

namespace DownfallArena.Cli.Table;

/// <summary>What the pilot posts to change who plays a seat: who takes it, and from which round.</summary>
internal sealed record TableSwapBody
{
    /// <summary>The agent that is to take the seat: an agent spec, or <c>person</c> for the seat's own player.</summary>
    [JsonPropertyName("agent")]
    public string? Agent { get; init; }

    /// <summary>
    /// The round the new occupant plays from. It must be one the match has not reached: a swap inside a round
    /// would split a sub-phase between two players.
    /// </summary>
    [JsonPropertyName("round")]
    public int? Round { get; init; }
}

using System.Text.Json;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// What a queue carries when a table's bots owe a turn (ADR 0159, phase 5): which table. Where its journal ends is
/// read by whoever takes the turn, from the journal itself, so an item delivered late takes the turn owed then.
/// </summary>
public sealed record OwedTurn(string Run)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, Web);

    /// <summary>The turn an item says is owed, or none when it is not one.</summary>
    public static OwedTurn? Parse(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<OwedTurn>(text, Web) is { Run: { Length: > 0 } } owed ? owed : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

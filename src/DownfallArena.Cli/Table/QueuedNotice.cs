using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One item of the notices queue (ADR 0203): a notice to one device of an account, or a "your turn" owed once its
/// grace is over. Exactly one of the two.
/// </summary>
internal sealed record QueuedNotice(DevicePush? Push, DueTurn? Turn)
{
    /// <summary>The queue the notices are put on; <c>notices-poison</c> and <c>notices-dlq</c> beside it.</summary>
    public const string Queue = "notices";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static QueuedNotice Of(DevicePush push) => new(push, null);

    public static QueuedNotice Of(DueTurn turn) => new(null, turn);

    public string ToJson() => JsonSerializer.Serialize(this, Web);

    /// <summary>The item, or none when the text is not one whole notice: exactly one part, and each part whole.</summary>
    public static QueuedNotice? Parse(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<QueuedNotice>(text, Web) switch
            {
                { Push: { Account.Length: > 0, Endpoint.Length: > 0, Payload.Length: > 0, Topic.Length: > 0 }, Turn: null } push => push,
                { Push: null, Turn: { Table.Length: > 0, Seat.Length: > 0 } } turn => turn,
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

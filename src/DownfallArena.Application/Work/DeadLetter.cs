using System.Text.Json;

namespace DownfallArena.Application.Work;

/// <summary>
/// An item kept for good once its worker gave up on it (ADR 0159, phase 5): everything its poison queue said of it,
/// and when and where it was set aside. Kept on a queue of its own, for ever, for an operator to read.
/// </summary>
/// <param name="Queue">The queue the item was first put on.</param>
/// <param name="Body">The item as it was put there.</param>
/// <param name="Error">Why its last delivery failed, as the worker that gave up on it said.</param>
/// <param name="Delivered">How many times it was delivered before it was given up on.</param>
/// <param name="Host">The machine that set it aside.</param>
public sealed record DeadLetter(
    string Queue,
    string Body,
    string Error,
    long Delivered,
    DateTimeOffset QueuedAt,
    DateTimeOffset PoisonedAt,
    DateTimeOffset DeadLetteredAt,
    string Host)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>The suffix of the queue the dead letters of a queue are kept on.</summary>
    public const string Suffix = "-dlq";

    /// <summary>The dead-letter queue of a queue.</summary>
    public static string QueueOf(string queue) => queue + Suffix;

    public string ToJson() => JsonSerializer.Serialize(this, Web);

    /// <summary>
    /// A dead letter as its queue holds it, or none when the text is not a whole one: an item with no queue or no body
    /// is something else put on the queue, listed and dropped as it is, never requeued.
    /// </summary>
    public static DeadLetter? Parse(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<DeadLetter>(text, Web) is { Queue.Length: > 0, Body.Length: > 0 } letter ? letter : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

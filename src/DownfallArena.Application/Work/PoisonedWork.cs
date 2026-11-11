using System.Text.Json;

namespace DownfallArena.Application.Work;

/// <summary>
/// An item a worker gave up on (ADR 0159, phase 5), as it goes on its poison queue: the item as it was, the queue it
/// came from, how many times it was delivered, and why the last delivery failed. What the first worker knows of the
/// failure travels with the item, since nothing else keeps it.
/// </summary>
public sealed record PoisonedWork(string Queue, string Body, string Error, long Delivered, DateTimeOffset QueuedAt, DateTimeOffset PoisonedAt)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>The suffix of the queue an item goes on once its worker gives up on it, after Azure Functions' own.</summary>
    public const string Suffix = "-poison";

    /// <summary>The poison queue of a queue.</summary>
    public static string QueueOf(string queue) => queue + Suffix;

    public string ToJson() => JsonSerializer.Serialize(this, Web);

    /// <summary>A poisoned item as its queue holds it, or none when the text is not one.</summary>
    public static PoisonedWork? Parse(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<PoisonedWork>(text, Web) is { Queue.Length: > 0 } poisoned ? poisoned : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

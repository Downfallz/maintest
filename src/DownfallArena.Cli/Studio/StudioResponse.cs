using System.Text;
using System.Text.Json;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// One HTTP answer of a host of this CLI: a status, a content type, the bytes to write, and the headers that
/// are not those — which is only ever <c>Location</c>, because a redirect is the one answer whose meaning is a
/// header rather than a body. Or, for the one route that does not end (<see cref="OfStream" />), a writer the
/// host hands the open response to, in place of the bytes.
/// </summary>
internal sealed record StudioResponse(int Status, string ContentType, byte[] Body, IReadOnlyList<KeyValuePair<string, string>>? Headers = null)
{
    /// <summary>
    /// Writes the answer itself, over as long as it likes, when it is not bytes known up front: the stream of
    /// a table's changes (ADR 0119). The host sends the status and headers, then hands this the body stream and
    /// closes it when the writer returns.
    /// </summary>
    public Func<Stream, CancellationToken, Task>? Writer { get; init; }

    public static StudioResponse OfStream(string contentType, Func<Stream, CancellationToken, Task> writer, IReadOnlyList<KeyValuePair<string, string>>? headers = null) =>
        new(200, contentType, [], headers) { Writer = writer };

    public const string Json = "application/json; charset=utf-8";

    public const string Html = "text/html; charset=utf-8";

    public const string Plain = "text/plain; charset=utf-8";

    public static StudioResponse OfJson<TPayload>(TPayload payload, JsonSerializerOptions options, int status = 200) =>
        new(status, Json, JsonSerializer.SerializeToUtf8Bytes(payload, options));

    public static StudioResponse OfText(int status, string contentType, string text) =>
        new(status, contentType, Encoding.UTF8.GetBytes(text));

    /// <summary>What the host says when there is nothing to render: a refusal, or a page that does not exist.</summary>
    public static StudioResponse OfPlainText(int status, string text) => OfText(status, Plain, text);

    /// <summary>
    /// Somewhere else, as a 303: the browser is told to read the answer with a GET, and the body is what a
    /// client that does not follow redirects is left with rather than nothing.
    /// </summary>
    public static StudioResponse OfRedirect(string location) =>
        new(303, Plain, Encoding.UTF8.GetBytes($"Continue at {location}"), [new KeyValuePair<string, string>("Location", location)]);
}

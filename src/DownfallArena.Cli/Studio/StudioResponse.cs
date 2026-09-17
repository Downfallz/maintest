using System.Text;
using System.Text.Json;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// One HTTP answer of a host of this CLI: a status, a content type, the bytes to write, and the headers that
/// are not those — which is only ever <c>Location</c>, because a redirect is the one answer whose meaning is a
/// header rather than a body.
/// </summary>
internal sealed record StudioResponse(int Status, string ContentType, byte[] Body, IReadOnlyList<KeyValuePair<string, string>>? Headers = null)
{
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

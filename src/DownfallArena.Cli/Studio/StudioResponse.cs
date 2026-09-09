using System.Text;
using System.Text.Json;

namespace DownfallArena.Cli.Studio;

/// <summary>One HTTP answer of the studio host: a status, a content type, and the bytes to write.</summary>
internal sealed record StudioResponse(int Status, string ContentType, byte[] Body)
{
    public const string Json = "application/json; charset=utf-8";

    public const string Html = "text/html; charset=utf-8";

    public static StudioResponse OfJson<TPayload>(TPayload payload, JsonSerializerOptions options, int status = 200) =>
        new(status, Json, JsonSerializer.SerializeToUtf8Bytes(payload, options));

    public static StudioResponse OfText(int status, string contentType, string text) =>
        new(status, contentType, Encoding.UTF8.GetBytes(text));
}

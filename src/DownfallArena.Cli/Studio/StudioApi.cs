using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// The studio's JSON API over one content directory: read everything, write one document, rewrite the aliases,
/// rebuild the consolidated schema, and run the engine. Content changes and runs are serialised, because they
/// share the files on disk and the page can fire them in any order.
/// </summary>
internal sealed class StudioApi : IDisposable
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ContentStore _store;
    private readonly StudioRunner _runner;
    private readonly string _schemaOutput;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public StudioApi(ContentStore store, StudioRunner runner, string schemaOutput)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaOutput);
        _store = store;
        _runner = runner;
        _schemaOutput = schemaOutput;
    }

    public async Task<StudioResponse> HandleAsync(string method, string path, string body)
    {
        await _gate.WaitAsync();
        try
        {
            return (method, path) switch
            {
                ("GET", "/api/catalogue") => Ok(_store.Read()),
                ("POST", "/api/documents") => SaveDocument(body),
                ("POST", "/api/documents/delete") => DeleteDocument(body),
                ("POST", "/api/aliases") => SaveAliases(body),
                ("POST", "/api/build") => Build(),
                ("POST", "/api/runs") => await RunAsync(body),
                _ => Failed(404, $"No such endpoint: {method} {path}."),
            };
        }
        // What the author got wrong answers 400. Anything else (a broken engine, a wiring mistake) is the host's
        // 500: telling an author their content is bad when the engine failed would send them hunting.
        catch (Exception exception) when (exception is InvalidGameContentException or ArgumentException or JsonException or IOException)
        {
            return Failed(400, exception.Message, Problems(exception));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The viewer page of a finished run, so the page opens on what the run did.</summary>
    public StudioResponse RunPage(string runId, string viewerDirectory)
    {
        try
        {
            var artifacts = _runner.Artifacts(runId);
            return artifacts.Count == 0
                ? StudioResponse.OfText(404, "text/plain; charset=utf-8", $"Run '{runId}' has no artifact to show.")
                : StudioResponse.OfText(200, StudioResponse.Html, ViewerPage.Render(viewerDirectory, runId, artifacts));
        }
        catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or FileNotFoundException or InvalidDataException)
        {
            return StudioResponse.OfText(404, "text/plain; charset=utf-8", exception.Message);
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private StudioResponse SaveDocument(string body)
    {
        var request = Parse<SaveDocumentRequest>(body);
        if (request.Document.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidGameContentException("A document must be a JSON object.");
        }

        var saved = _store.Save(KindOf(request.Kind), request.Path, request.Document.GetRawText());
        return Ok(new { saved, catalogue = _store.Read() });
    }

    private StudioResponse DeleteDocument(string body)
    {
        var request = Parse<DeleteDocumentRequest>(body);
        _store.Delete(KindOf(request.Kind), request.Path);
        return Ok(new { deleted = request.Path, catalogue = _store.Read() });
    }

    private StudioResponse SaveAliases(string body)
    {
        var request = Parse<SaveAliasesRequest>(body);
        _store.SaveAliases(request.Aliases);
        return Ok(new { catalogue = _store.Read() });
    }

    private StudioResponse Build()
    {
        try
        {
            var (contentHash, notes) = _store.Build(_schemaOutput);
            return Ok(new { contentHash, notes, output = _schemaOutput, catalogue = _store.Read() });
        }
        catch (InvalidGameContentException exception)
        {
            return Failed(400, "The content does not build yet.", [.. exception.Problems]);
        }
    }

    private async Task<StudioResponse> RunAsync(string body) => Ok(await _runner.RunAsync(Parse<StudioRunRequest>(body)));

    private static TRequest Parse<TRequest>(string body)
        where TRequest : class
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new JsonException("The request has no body.");
        }

        return JsonSerializer.Deserialize<TRequest>(body, JsonOptions) ?? throw new JsonException("The request body is empty.");
    }

    private static StudioResponse Ok<TPayload>(TPayload payload) => StudioResponse.OfJson(new { ok = true, result = payload }, JsonOptions);

    private static StudioResponse Failed(int status, string message, IReadOnlyList<string>? problems = null) =>
        StudioResponse.OfJson(new { ok = false, message, problems = problems ?? Array.Empty<string>() }, JsonOptions, status);

    /// <summary>
    /// <c>ContentKind.Creature</c> is the enum's zero, so a request that omits or misspells <c>kind</c> would
    /// otherwise mean "creature" and fail later with a message about the wrong folder.
    /// </summary>
    private static ContentKind KindOf(ContentKind? kind) =>
        kind ?? throw new InvalidGameContentException($"'kind' is required, and one of {string.Join(", ", Enum.GetNames<ContentKind>())}.");

    private static IReadOnlyList<string> Problems(Exception exception) =>
        exception is InvalidGameContentException invalid ? invalid.Problems : [];

    private sealed record SaveDocumentRequest
    {
        public ContentKind? Kind { get; init; }

        public string Path { get; init; } = string.Empty;

        public JsonElement Document { get; init; }
    }

    private sealed record DeleteDocumentRequest
    {
        public ContentKind? Kind { get; init; }

        public string Path { get; init; } = string.Empty;
    }

    private sealed record SaveAliasesRequest
    {
        public IReadOnlyDictionary<string, string> Aliases { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}

using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Evaluation;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// The studio's JSON API over one content directory: read everything, write one document, rewrite the aliases,
/// rebuild the consolidated schema, audit what no creature can reach, list what has been played, and run the
/// engine. Content changes and runs are serialised, because they share the files on disk and the page can fire
/// them in any order.
/// </summary>
internal sealed class StudioApi : IDisposable
{
    private readonly ContentStore _store;
    private readonly StudioRunner _runner;
    private readonly BenchmarkStore _benchmarks;
    private readonly RuleSet _rules;
    private readonly string _schemaOutput;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public StudioApi(ContentStore store, StudioRunner runner, BenchmarkStore benchmarks, RuleSet rules, string schemaOutput)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(benchmarks);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaOutput);
        _store = store;
        _runner = runner;
        _benchmarks = benchmarks;
        _rules = rules;
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
                ("GET", "/api/audit") => Audit(),
                ("GET", "/api/runs") => Ok(_runner.Runs()),
                ("GET", "/api/weights") => Ok(Weights()),
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

    /// <summary>
    /// The viewer page of two finished runs at once, so the page opens on what changed between them. Same seeds
    /// and same agents on both sides is what makes the delta about the content; the page cannot check that for
    /// the author, but the run list it picks from says the seed and the agents of each.
    /// </summary>
    public StudioResponse ComparePage(string first, string second, string viewerDirectory)
    {
        if (string.Equals(first, second, StringComparison.Ordinal))
        {
            return StudioResponse.OfText(400, "text/plain; charset=utf-8", "A run compared with itself has no delta. Pick two runs.");
        }

        try
        {
            List<(string Name, string Text)> artifacts = [.. _runner.Artifacts(first, first), .. _runner.Artifacts(second, second)];
            return artifacts.Count < 2
                ? StudioResponse.OfText(404, "text/plain; charset=utf-8", $"'{first}' and '{second}' do not both have an artifact to compare.")
                : StudioResponse.OfText(200, StudioResponse.Html, ViewerPage.Render(viewerDirectory, $"{first} vs {second}", artifacts));
        }
        catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or FileNotFoundException or InvalidDataException)
        {
            return StudioResponse.OfText(404, "text/plain; charset=utf-8", exception.Message);
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

        var saved = _store.Save(KindOf(request.Kind), request.Path, request.Document.GetRawText(), overwrite: !request.Create);
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

    /// <summary>
    /// What no creature can reach, open or cast, plus whether this content has a benchmark digest. Editing
    /// content changes its hash, and a digest is filed under the hash it was measured on, so the answer is
    /// usually no right after an edit — which is worth saying rather than leaving the author to assume the net
    /// is still there.
    /// </summary>
    private StudioResponse Audit()
    {
        var report = _store.Audit(_rules);
        var digest = _benchmarks.DigestPath(report.ContentVersion);
        return Ok(new
        {
            audit = report,
            benchmark = new { path = digest, exists = File.Exists(digest) },
        });
    }

    /// <summary>
    /// The built-in scoring weights, so the run panel's sliders start from the engine's values and grow a field
    /// when the engine grows a weight, rather than from a copy of the numbers in the page.
    /// </summary>
    private static object Weights()
    {
        var weights = ScoringWeights.Default;
        return new
        {
            values = weights.Named.ToDictionary(weight => weight.Name, weight => weight.Value, StringComparer.Ordinal),
            order = weights.Named.Select(weight => weight.Name),
            fingerprint = weights.Fingerprint,
        };
    }

    private static TRequest Parse<TRequest>(string body)
        where TRequest : class
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new JsonException("The request has no body.");
        }

        return JsonSerializer.Deserialize<TRequest>(body, StudioJson.Options) ?? throw new JsonException("The request body is empty.");
    }

    private static StudioResponse Ok<TPayload>(TPayload payload) => StudioResponse.OfJson(new { ok = true, result = payload }, StudioJson.Options);

    private static StudioResponse Failed(int status, string message, IReadOnlyList<string>? problems = null) =>
        StudioResponse.OfJson(new { ok = false, message, problems = problems ?? [] }, StudioJson.Options, status);

    /// <summary>
    /// <c>ContentKind.Creature</c> is the enum's zero, so a request that omits or misspells <c>kind</c> would
    /// otherwise mean "creature" and fail later with a message about the wrong folder.
    /// </summary>
    private static ContentKind KindOf(ContentKind? kind) =>
        kind ?? throw new InvalidGameContentException($"'kind' is required, and one of {string.Join(", ", Enum.GetNames<ContentKind>())}.");

    private static IReadOnlyList<string> Problems(Exception exception) =>
        exception is InvalidGameContentException invalid ? invalid.Problems : [];
}

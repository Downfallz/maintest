using System.Text.Json;
using DownfallArena.Cli.Studio;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Table;

/// <summary>Serializes requests with restarts so no query uses a disposed match gate.</summary>
internal sealed class PracticeTable(CliOptions options, string token) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PracticeRun? _run;

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string? supplied, string? ifNoneMatch = null, string? query = null)
    {
        await _gate.WaitAsync();
        try
        {
            if (path == "/api/practice")
            {
                if (!string.Equals(supplied, token, StringComparison.Ordinal))
                {
                    return StudioResponse.OfPlainText(403, "Open the practice link printed by the host.");
                }
                return method switch
                {
                    "GET" => State(),
                    "POST" => await StartAsync(body),
                    _ => StudioResponse.OfPlainText(404, "No such practice route."),
                };
            }

            return _run is { } run
                ? await run.Api.HandleAsync(method, path, body, supplied, ifNoneMatch, query)
                : StudioResponse.OfPlainText(409, "Choose a practice scenario first.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private StudioResponse State() => StudioResponse.OfJson(new
    {
        scenarios = PracticeScenario.All,
        active = _run?.Scenario.Id,
        seatToken = _run?.Token,
        seed = PracticeScenario.Seed,
        recording = false,
    }, ArtifactJson.LineOptions);

    private async Task<StudioResponse> StartAsync(string body)
    {
        string? id;
        try
        {
            using var posted = JsonDocument.Parse(body);
            id = posted.RootElement.ValueKind == JsonValueKind.Object && posted.RootElement.TryGetProperty("scenario", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return StudioResponse.OfPlainText(400, "Choose a scenario from the practice list.");
        }

        var scenario = PracticeScenario.All.FirstOrDefault(one => one.Id == id);
        if (scenario is null)
        {
            return StudioResponse.OfPlainText(400, "Unknown practice scenario.");
        }
        PracticeRun next;
        try
        {
            next = await PracticeRun.StartAsync(options, scenario);
        }
        catch (Exception exception) when (exception is InvalidDataException or OperationCanceledException)
        {
            return StudioResponse.OfPlainText(409, $"The scenario could not be prepared: {exception.Message}");
        }

        var previous = _run;
        _run = next;
        if (previous is not null)
        {
            await previous.DisposeAsync();
        }
        return State();
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_run is not null)
            {
                await _run.DisposeAsync();
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}

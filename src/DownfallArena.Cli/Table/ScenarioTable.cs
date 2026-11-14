using DownfallArena.Cli.Studio;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches.Scenarios;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The practice table of <c>table --scenario</c> (docs/tabletop/scenarios-plan.md, S3): the practice page's one
/// scenario, started when the host starts and started again from its cut whenever the page restarts it. Requests are
/// serialized with restarts, as the recipes' table does, so none uses a playthrough being let go of.
/// </summary>
internal sealed class ScenarioTable(CliOptions options, string schema, ScenarioDocument document, string token) : IPracticeTable
{
    private const string Id = "scenario";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private ScenarioRun? _run;

    public async Task StartAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await RestartAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

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

                switch (method)
                {
                    case "GET":
                        return State();
                    case "POST":
                        try
                        {
                            await RestartAsync();
                        }
                        catch (Exception exception) when (exception is InvalidDataException or OperationCanceledException)
                        {
                            return StudioResponse.OfPlainText(409, $"The scenario could not be started again: {exception.Message}");
                        }

                        return State();
                    default:
                        return StudioResponse.OfPlainText(404, "No such practice route.");
                }
            }

            return _run is { } run
                ? await run.Api.HandleAsync(method, path, body, supplied, ifNoneMatch, query)
                : StudioResponse.OfPlainText(409, "The scenario has not started.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>What the practice page reads: the one scenario, named and described, and the seat it is played from.</summary>
    private StudioResponse State()
    {
        var cut = document.Scenario.Cut;
        var seat = ScenarioCommands.SeatName(document.Seat);
        return StudioResponse.OfJson(new
        {
            scenarios = new[]
            {
                new
                {
                    id = Id,
                    name = document.Title ?? $"Round {cut.Round} of session {document.Session ?? document.Scenario.Source.Run}",
                    goal = document.Goal ?? $"Played as {seat} from the top of round {cut.Round}. The opponent replays its recorded answers while they still fit, then greedy plays it. The recorded match is unchanged.",
                },
            },
            active = _run is null ? null : Id,
            seatToken = _run?.Token,
            seat,
            seed = document.Scenario.Source.Seed,
            recording = false,
        }, ArtifactJson.LineOptions);
    }

    private async Task RestartAsync()
    {
        var next = await ScenarioRun.StartAsync(options, schema, document);
        var previous = _run;
        _run = next;
        if (previous is not null)
        {
            await previous.DisposeAsync();
        }
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

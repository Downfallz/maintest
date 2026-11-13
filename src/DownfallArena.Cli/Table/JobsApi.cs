using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Application.Tables.Ports;
using DownfallArena.Application.Tables.Tending;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The operator's jobs (docs/tabletop/admin-plan.md, A3): the tending runs, newest first, twenty a page with what the
/// last day and the last week of them did, one run's sheet, and a pass run now from this process, kept like the hourly
/// job's, marked manual with the operator's name. Reached through <see cref="AdminApi" />, behind its gate.
/// </summary>
internal sealed class JobsApi
{
    public const string Prefix = "/api/admin/jobs";

    public const string TendRoute = Prefix + "/tend";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    // One pass at a time from this process: a second click while one runs is told so rather than queued behind it.
    private static readonly SemaphoreSlim Passing = new(1, 1);

    private readonly ITendLog _log;
    private readonly TendRuns _runs;
    private readonly TimeProvider _clock;
    private readonly Func<DateTimeOffset, Task<TableTend.Tended>> _pass;

    /// <param name="pass">The tending pass this process runs, the one <c>table-tend</c> runs, over its own tables.</param>
    public JobsApi(ITendLog log, TimeProvider clock, Func<DateTimeOffset, Task<TableTend.Tended>> pass)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(pass);
        _log = log;
        _runs = new TendRuns(log);
        _clock = clock;
        _pass = pass;
    }

    public static bool Names(string path) => path == Prefix || path.StartsWith(Prefix + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, string? query, string operatorName)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path == TendRoute)
        {
            return method switch
            {
                "GET" => await PageAsync(query),
                "POST" => await PassAsync(operatorName),
                _ => Problem(405, "Jobs.NoRoute", $"No such route: {method} {path}"),
            };
        }

        if (method == "GET" && path.StartsWith(TendRoute + "/", StringComparison.Ordinal))
        {
            var id = Uri.UnescapeDataString(path[(TendRoute.Length + 1)..]);
            return await _runs.FindAsync(id) is { } run
                ? StudioResponse.OfJson(new { run = Describe(run) }, Web)
                : Problem(404, "Jobs.NoRun", "No tending run is kept under that id.");
        }

        return Problem(404, "Jobs.NoRoute", $"No such route: {method} {path}");
    }

    /// <summary>A run as the panel reads it: the record, with how long it took and what it touched counted.</summary>
    public static object Describe(TendRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new
        {
            run.Id,
            run.Started,
            run.Finished,
            seconds = Math.Round((run.Finished - run.Started).TotalSeconds, 1),
            run.Trigger,
            run.By,
            run.Outcome,
            run.Error,
            run.NeverBegan,
            run.InvitationsExpired,
            run.TimedOut,
            run.Abandoned,
            run.Warned,
        };
    }

    private async Task<StudioResponse> PageAsync(string? query)
    {
        var asked = System.Web.HttpUtility.ParseQueryString(query ?? string.Empty);
        var page = await _runs.PageAsync(int.TryParse(asked["page"], out var number) ? number : 1, _clock.GetUtcNow());
        return StudioResponse.OfJson(
            new
            {
                runs = page.Runs.Select(Describe).ToArray(),
                total = page.Total,
                page = page.Page,
                pages = page.Pages,
                perPage = TendRuns.PerPage,
                day = page.Day,
                week = page.Week,
            },
            Web);
    }

    private async Task<StudioResponse> PassAsync(string operatorName)
    {
        if (!await Passing.WaitAsync(TimeSpan.Zero))
        {
            return Problem(409, "Jobs.Passing", "A pass is already running. Its run shows here once it ends.");
        }

        try
        {
            var (_, run) = await TableTend.RecordedAsync(_log, TendTrigger.Manual, operatorName, _clock, _pass);
            Console.WriteLine($"  Tending pass {run.Id} run by {operatorName}: {run.NeverBegan.Count} closed, {run.InvitationsExpired} invitations let go of, {run.TimedOut.Count} timed out, {run.Abandoned.Count} left ended, {run.Warned.Count} warned.");
            return StudioResponse.OfJson(new { run = Describe(run) }, Web);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure))
        {
            return Problem(503, "Jobs.Failed", $"The pass stopped: {failure.Message}. Its run is kept as failed.");
        }
        finally
        {
            Passing.Release();
        }
    }

    private static StudioResponse Problem(int status, string code, string message) =>
        StudioResponse.OfJson(new { error = code, message }, Web, status);
}

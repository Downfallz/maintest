using System.Text.Json;
using DownfallArena.Application.Work;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The notices in the operator's Jobs tab (notifications plan, N6): the three queues a notice goes through, each one's
/// length and oldest item, and what the notices worker sent, had refused and found gone, a kind each, today and over
/// the last seven days, whichever process sent them. Reached through <see cref="AdminApi" />, behind its gate; on a host
/// that sends its notices in the request there is no queue, and none of this.
/// </summary>
internal sealed class NoticesApi(TurnQueues queues, NoticeCounts? counts)
{
    public const string Prefix = JobsApi.Prefix + "/notices";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static bool Names(string path) => path == Prefix || path.StartsWith(Prefix + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path)
    {
        if (method != "GET" || path != Prefix)
        {
            return Problem(404, "Notices.NoRoute", $"No such route: {method} {path}");
        }

        try
        {
            var states = await queues.StatesAsync();
            var week = counts is null ? null : await counts.WeekAsync();
            return StudioResponse.OfJson(
                new
                {
                    queues = states.Select(state => new { state.Name, state.Count, state.Oldest }).ToArray(),
                    counted = week is not null,
                    today = week?.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    day = week?.Day.Select(Kind).ToArray() ?? [],
                    week = week?.Week.Select(Kind).ToArray() ?? [],
                    days = NoticeCounts.Days,
                },
                Web);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure))
        {
            await Console.Error.WriteLineAsync($"  The notices' queues or counts could not be read: {Hosting.HttpHost.OneLine(failure)}");
            return Problem(503, "Notices.Unavailable", "The notices' queues or counts could not be read. Try again in a moment.");
        }
    }

    private static object Kind(NoticeCount count) => new { count.Kind, count.Sent, count.Refused, count.Gone };

    private static StudioResponse Problem(int status, string code, string message) =>
        StudioResponse.OfJson(new { error = code, message }, Web, status);
}

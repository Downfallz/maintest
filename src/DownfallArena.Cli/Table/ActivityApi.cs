using System.Text.Json;
using DownfallArena.Application.Admin;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Headquarters' Activity tab (admin plan, A6): what operators did from the panel, newest first, twenty a page, from the
/// operator log. Reached through <see cref="AdminApi" />, behind its gate.
/// </summary>
internal sealed class ActivityApi(OperatorActions actions)
{
    public const string Route = "/api/admin/activity";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static bool Names(string path) => path == Route;

    public async Task<StudioResponse> HandleAsync(string method, string? query)
    {
        if (method != "GET")
        {
            return StudioResponse.OfJson(new { error = "Activity.NoRoute", message = $"No such route: {method} {Route}" }, Web, status: 405);
        }

        var asked = System.Web.HttpUtility.ParseQueryString(query ?? string.Empty);
        var page = await actions.PageAsync(int.TryParse(asked["page"], out var number) ? number : 1);
        return StudioResponse.OfJson(
            new
            {
                actions = page.Actions.Select(action => new { action.Id, action.At, action.By, action.Action, action.Target, action.Detail }).ToArray(),
                page.Total,
                page.Page,
                page.Pages,
                perPage = OperatorActions.PerPage,
                kept = OperatorAction.Kept,
            },
            Web);
    }
}

using System.Text.Json;
using DownfallArena.Application.Admin;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches.Scenarios;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The operator's side of the match scenarios (docs/tabletop/scenarios-plan.md, S4, ADR 0179), behind the admin
/// panel's gate: <c>GET /api/admin/scenarios</c> lists them, <c>POST</c> with
/// <c>{"session","round","seat?","title?","goal?"}</c> cuts a recorded session at the top of a round, on the content it
/// was played on, and keeps it, and <c>DELETE /api/admin/scenarios/&lt;id&gt;</c> removes one. A scenario's link,
/// <c>/battle?scenario=&lt;id&gt;</c>, is what a signed-in player opens.
/// </summary>
internal sealed class ScenarioAdminApi(ScenarioStore store, StoredSessions stored, TableComposer composer)
{
    public const string Route = "/api/admin/scenarios";

    /// <summary>What operators do from the panel (A6): a scenario cut or removed is noted with the rest.</summary>
    public OperatorNotes Notes { get; set; } = OperatorNotes.None;

    public static bool Names(string path) => path == Route || path.StartsWith(Route + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string operatorName)
    {
        var rest = path[Route.Length..].Trim('/');
        return (method, rest) switch
        {
            ("GET", "") => await ListAsync(),
            ("POST", "") => await CutAsync(body, operatorName),
            ("DELETE", { Length: > 0 } id) => await DeleteAsync(id, operatorName),
            _ => Error("Scenario.NoRoute", $"No such route: {method} {path}", 404),
        };
    }

    private async Task<StudioResponse> ListAsync()
    {
        var kept = await store.ListAsync();
        return StudioResponse.OfJson(new
        {
            scenarios = kept.Select(one => new
            {
                id = one.Id,
                title = one.Document.Title,
                goal = one.Document.Goal,
                session = one.Document.Session,
                round = one.Document.Scenario.Cut.Round,
                seat = ScenarioCommands.SeatName(one.Document.Seat),
                content = one.Document.Content,
                link = Link(one.Id),
            }).ToArray(),
        }, ArtifactJson.LineOptions);
    }

    private async Task<StudioResponse> CutAsync(string body, string operatorName)
    {
        Asked? asked;
        try
        {
            asked = JsonSerializer.Deserialize<Asked>(body, ArtifactJson.LineOptions);
        }
        catch (JsonException)
        {
            asked = null;
        }

        if (asked is not { Session: { Length: > 0 } session, Round: > 0 } || asked.Seat is not (null or "player1" or "player2")
            || asked.Title is { Length: > 80 } || asked.Goal is { Length: > 300 })
        {
            return Error("Scenario.Invalid", "Name a recorded session and a round from 1; the seat is player1 or player2, the title 80 characters at most and the goal 300.", 400);
        }

        if (!TableKeys.IsTableId(session) || await stored.RecordAsync(session) is not { } record)
        {
            return Error("Scenario.NoSession", $"No recorded session '{session}' at this host.", 404);
        }

        if (record.Rules is not { } recorded)
        {
            return Error("Scenario.NoRules", "This session was recorded before its rules were kept with it (ADR 0163): it cannot be replayed on the rules it began on.", 409);
        }

        var (content, _) = await composer.PlayedOnAsync(record.Content);
        if (record.Content is { } version && !string.Equals(content.Version, version, StringComparison.Ordinal))
        {
            return Error("Scenario.ContentGone", $"The content this session was played on ({version}) is not kept at this host.", 409);
        }

        var rules = recorded.ToRuleSet();
        var resources = content.Services.GetRequiredService<IGameResources>();
        var entries = (await composer.JournalAsync(session))?.Entries ?? [];
        var source = new JournaledMatch(session, record.MatchId, rules, record.Seed, [.. Enumerable.Repeat(resources.Creatures.First().Id, rules.TeamSize)]);
        var cut = await content.Services.GetRequiredService<ScenarioCutter>().CutAsync(source, entries, asked.Round);
        if (cut.IsFailure)
        {
            var offered = await content.Services.GetRequiredService<ScenarioRounds>().ReadAsync(source, entries);
            return StudioResponse.OfJson(new { error = cut.Error.Code, message = cut.Error.Message, rounds = offered.Rounds.Select(one => one.Round).ToArray() }, ArtifactJson.LineOptions, 409);
        }

        var seat = asked.Seat == "player2" ? PlayerSlot.Player2 : PlayerSlot.Player1;
        var id = await store.PutAsync(new ScenarioDocument(cut.Value, seat, resources.Version, session, Application.Learning.EngineVersion.Current.ToString(), asked.Title, asked.Goal));
        Console.WriteLine($"  Scenario {id} cut from session {session} at round {asked.Round} by {operatorName}.");
        await Notes.NoteAsync(operatorName, OperatorActions.CutScenario, id, $"session {session}, round {asked.Round}, {ScenarioCommands.SeatName(seat)}");
        return StudioResponse.OfJson(new { id, link = Link(id) }, ArtifactJson.LineOptions, 201);
    }

    private async Task<StudioResponse> DeleteAsync(string id, string operatorName)
    {
        if (!await store.DeleteAsync(id))
        {
            return Error("Scenario.NotFound", $"No scenario '{id}' at this host.", 404);
        }

        Console.WriteLine($"  Scenario {id} removed by {operatorName}.");
        await Notes.NoteAsync(operatorName, OperatorActions.RemoveScenario, id);
        return new StudioResponse(204, StudioResponse.Plain, []);
    }

    private static string Link(string id) => $"/battle?scenario={id}";

    private static StudioResponse Error(string error, string message, int status) =>
        StudioResponse.OfJson(new { error, message }, ArtifactJson.LineOptions, status);

    private sealed record Asked(string? Session, int Round, string? Seat, string? Title, string? Goal);
}

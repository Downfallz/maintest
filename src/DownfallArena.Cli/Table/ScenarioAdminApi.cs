using System.Text.Json;
using DownfallArena.Application.Admin;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.SharedKernel.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The operator's side of the match scenarios (docs/tabletop/scenarios-plan.md, S4, ADR 0179), behind the admin
/// panel's gate: <c>GET /api/admin/scenarios</c> lists them, <c>POST</c> with
/// <c>{"session","round","seat?","title?","goal?"}</c> cuts a recorded session at the top of a round, on the content it
/// was played on, and keeps it, and <c>DELETE /api/admin/scenarios/&lt;id&gt;</c> removes one; <c>PUT .../&lt;id&gt;/listing {"listed"}</c> lists it under
/// Practice for signed-in players, or takes it off (S6). A scenario's link,
/// <c>/battle?scenario=&lt;id&gt;</c>, is what a signed-in player opens. The scenarios the repository ships (S7) are
/// listed first, each with the file it is; one is neither removed nor listed here (409).
/// </summary>
internal sealed class ScenarioAdminApi(ScenarioStore store, StoredSessions stored, TableComposer composer)
{
    public const string Route = "/api/admin/scenarios";

    private static readonly DomainError UnderWay = new("Scenario.UnderWay", "This session is still being played: it can be cut once it is over, so nothing a seat holds face down reaches a scenario.");

    /// <summary>What operators do from the panel (A6): a scenario cut or removed is noted with the rest.</summary>
    public OperatorNotes Notes { get; set; } = OperatorNotes.None;

    public static bool Names(string path) => path == Route || path.StartsWith(Route + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string operatorName, string? query = null)
    {
        var rest = path[Route.Length..].Trim('/');
        return (method, rest) switch
        {
            ("GET", "") => await ListAsync(),
            ("GET", "rounds") => await RoundsAsync(System.Web.HttpUtility.ParseQueryString(query ?? string.Empty)["session"]),
            ("POST", "") => await CutAsync(body, operatorName),
            ("DELETE", { Length: > 0 } id) => await DeleteAsync(id, operatorName),
            ("PUT", { Length: > 0 } listing) when listing.EndsWith("/listing", StringComparison.Ordinal) => await ListAsync(listing[..^"/listing".Length], body, operatorName),
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
                listed = one.Listed,
                source = one.File is null ? "cut" : "repository",
                file = one.File,

                // Its place in the series when it is a tutorial (ADR 0187), which every signed-in account plays.
                tutorial = one.Document.Tutorial?.Order,
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

        if (asked is not { Session: { Length: > 0 } session, Round: > 0 } || (asked.Seat is not null && SlotOf(asked.Seat) is null)
            || asked.Title is { Length: > 80 } || asked.Goal is { Length: > 300 })
        {
            return Error("Scenario.Invalid", "Name a recorded session and a round from 1; the seat is player1 or player2, the title 80 characters at most and the goal 300.", 400);
        }

        var read = await SourceAsync(session);
        if (read.Refused is { } refused)
        {
            return refused;
        }

        var seat = SlotOf(asked.Seat) ?? PlayerSlot.Player1;
        var kept = await KeepAsync(read.Source!.Value, asked.Round, seat, session, asked.Title, asked.Goal);
        if (kept.Refused is { } notCut)
        {
            return notCut;
        }

        var id = kept.Id!;
        Console.WriteLine($"  Scenario {id} cut from session {session} at round {asked.Round} by {operatorName}.");
        await Notes.NoteAsync(operatorName, OperatorActions.CutScenario, id, $"session {session}, round {asked.Round}, {ScenarioCommands.SeatName(seat)}");
        return StudioResponse.OfJson(new { id, link = Link(id) }, ArtifactJson.LineOptions, 201);
    }

    /// <summary>
    /// The scenario a bug report asks for (docs/tabletop/feedback-plan.md, R4): cut at the top of the reported round, in
    /// the reporter's seat, from the table's recorded session, or, for a report sent from a practice, from the scenario
    /// it was opened from followed by the entries the report copied. Its title names the report and its goal is empty,
    /// so no player's words reach a file that may become public. The scenario kept, or the answer that says why it is
    /// not; the caller notes it.
    /// </summary>
    public async Task<(StudioResponse? Refused, string? Id)> ReproduceAsync(FeedbackContext context, string report)
    {
        ArgumentNullException.ThrowIfNull(context);
        // The seat is the reporter's: a cut in the other one would have the bot replay the reporter's own answers.
        if (context.Round is not { } round || round < 1 || (context.Table is null && context.Scenario is null) || SlotOf(context.Seat) is not { } seat)
        {
            return (Error(FeedbackErrors.NoPosition.Code, FeedbackErrors.NoPosition.Message, 409), null);
        }

        var read = context.Table is { } table ? await SourceAsync(table, FeedbackErrors.UnderWay) : await PracticeSourceAsync(context.Scenario!, context.PracticeEntries ?? []);
        if (read.Refused is { } refused)
        {
            return (refused, null);
        }

        return await KeepAsync(read.Source!.Value, round, seat, context.Table, $"Report {report}, round {round}", goal: null);
    }

    /// <summary>The source cut at the top of a round and kept, or the rounds it offers when that one is not among them.</summary>
    private async Task<(StudioResponse? Refused, string? Id)> KeepAsync((TableContent Content, JournaledMatch Match, IReadOnlyList<IJournalEntry> Entries) read, int round, PlayerSlot seat, string? session, string? title, string? goal)
    {
        var (content, source, entries) = read;
        var resources = content.Services.GetRequiredService<IGameResources>();
        var cut = await content.Services.GetRequiredService<ScenarioCutter>().CutAsync(source, entries, round);
        if (cut.IsFailure)
        {
            var offered = await content.Services.GetRequiredService<ScenarioRounds>().ReadAsync(source, entries);
            return (StudioResponse.OfJson(new { error = cut.Error.Code, message = cut.Error.Message, rounds = offered.Rounds.Select(one => one.Round).ToArray() }, ArtifactJson.LineOptions, 409), null);
        }

        return (null, await store.PutAsync(new ScenarioDocument(cut.Value, seat, resources.Version, session, Application.Learning.EngineVersion.Current.ToString(), title, goal)));
    }

    /// <summary>
    /// A practice as a scenario is cut from it: the scenario it was opened from, rebuilt from its own match with the
    /// entries before its cut, as the practice's journal began, then the entries the report copied.
    /// </summary>
    private async Task<(StudioResponse? Refused, (TableContent Content, JournaledMatch Match, IReadOnlyList<IJournalEntry> Entries)? Source)> PracticeSourceAsync(string scenario, IReadOnlyList<JsonElement> copied)
    {
        if (await store.GetAsync(scenario) is not { } document)
        {
            // 409, not 404: the report is there, the scenario its practice was opened from is not.
            return (Error("Scenario.NotFound", $"The scenario '{scenario}' this practice was opened from is no longer kept at this host.", 409), null);
        }

        var (content, _) = await composer.PlayedOnAsync(document.Content);
        if (!string.Equals(content.Version, document.Content, StringComparison.Ordinal))
        {
            return (Error("Scenario.ContentGone", $"The content this practice was played on ({document.Content}) is not kept at this host.", 409), null);
        }

        IReadOnlyList<IJournalEntry> since;
        try
        {
            since = ScenarioFile.EntriesOf(copied);
        }
        catch (InvalidDataException failure)
        {
            Console.WriteLine($"  The entries a practice's report copied cannot be read: {failure.Message}");
            return (Error("Scenario.Invalid", "The entries this report copied from its practice cannot be read.", 409), null);
        }

        // The practice's journal began with the scenario's own entries, a seat that changed hands at the source left out.
        var before = document.Scenario.Before.Where(entry => entry is not SeatChangeEntry);
        return (null, (content, document.Scenario.Source, [.. before, .. since]));
    }

    /// <summary>The rounds a recorded session can be cut at, and why its journal stopped rebuilding when it did.</summary>
    private async Task<StudioResponse> RoundsAsync(string? session)
    {
        var read = await SourceAsync(session ?? string.Empty);
        if (read.Refused is { } refused)
        {
            return refused;
        }

        var (content, source, entries) = read.Source!.Value;
        var reading = await content.Services.GetRequiredService<ScenarioRounds>().ReadAsync(source, entries);
        return StudioResponse.OfJson(new { session, rounds = reading.Rounds.Select(one => one.Round).ToArray(), stoppedBy = reading.StoppedBy?.Message }, ArtifactJson.LineOptions);
    }

    /// <summary>
    /// A recorded session as a scenario is cut from it: the content it was played on, its match as it was created, and
    /// its journal; or the answer that says why it cannot be.
    /// </summary>
    private async Task<(StudioResponse? Refused, (TableContent Content, JournaledMatch Match, IReadOnlyList<IJournalEntry> Entries)? Source)> SourceAsync(string session, DomainError? underWay = null)
    {
        if (!TableKeys.IsTableId(session) || await stored.RecordAsync(session) is not { } record)
        {
            return (Error("Scenario.NoSession", $"No recorded session '{session}' at this host.", 404), null);
        }

        // A table still played is cut once it is over (#736): its journal holds the other seat's choices of the round,
        // face down, which the scenario's opponent would follow and anyone holding the scenario's id would open.
        if (record is { Status: TableRecord.Open, Void: false })
        {
            var refused = underWay ?? UnderWay;
            return (Error(refused.Code, refused.Message, 409), null);
        }

        if (record.Rules is not { } recorded)
        {
            return (Error("Scenario.NoRules", "This session was recorded before its rules were kept with it (ADR 0163): it cannot be replayed on the rules it began on.", 409), null);
        }

        var (content, _) = await composer.PlayedOnAsync(record.Content);
        if (record.Content is { } version && !string.Equals(content.Version, version, StringComparison.Ordinal))
        {
            return (Error("Scenario.ContentGone", $"The content this session was played on ({version}) is not kept at this host.", 409), null);
        }

        var rules = recorded.ToRuleSet();
        var resources = content.Services.GetRequiredService<IGameResources>();
        var entries = (await composer.JournalAsync(session))?.Entries ?? [];
        var match = new JournaledMatch(session, record.MatchId, rules, record.Seed, [.. Enumerable.Repeat(resources.Creatures.First().Id, rules.TeamSize)]);
        return (null, (content, match, entries));
    }

    private async Task<StudioResponse> ListAsync(string id, string body, string operatorName)
    {
        bool? listed;
        try
        {
            using var asked = JsonDocument.Parse(body);
            listed = asked.RootElement.ValueKind == JsonValueKind.Object && asked.RootElement.TryGetProperty("listed", out var value)
                && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
        }
        catch (JsonException)
        {
            listed = null;
        }

        if (listed is not { } list)
        {
            return Error("Scenario.Invalid", "Say whether the scenario is listed: {\"listed\":true} or {\"listed\":false}.", 400);
        }

        if (store.Ships(id))
        {
            return Error("Scenario.FromRepository", store.Teaches(id) ? "This tutorial comes from the repository and is offered to every signed-in account: it cannot be listed under Practice." : "This scenario comes from the repository and is offered in the admin panel only: it cannot be listed under Practice.", 409);
        }

        if (!await store.SetListedAsync(id, list))
        {
            return Error("Scenario.NotFound", $"No scenario '{id}' at this host.", 404);
        }

        Console.WriteLine($"  Scenario {id} {(list ? "listed under Practice" : "taken off Practice")} by {operatorName}.");
        await Notes.NoteAsync(operatorName, list ? OperatorActions.ListScenario : OperatorActions.UnlistScenario, id);
        return StudioResponse.OfJson(new { id, listed = list }, ArtifactJson.LineOptions);
    }

    private async Task<StudioResponse> DeleteAsync(string id, string operatorName)
    {
        if (store.Ships(id))
        {
            return Error("Scenario.FromRepository", "This scenario comes from the repository: remove its file from scenarios/ there, and it is gone at the next deploy.", 409);
        }

        if (!await store.DeleteAsync(id))
        {
            return Error("Scenario.NotFound", $"No scenario '{id}' at this host.", 404);
        }

        Console.WriteLine($"  Scenario {id} removed by {operatorName}.");
        await Notes.NoteAsync(operatorName, OperatorActions.RemoveScenario, id);
        return new StudioResponse(204, StudioResponse.Plain, []);
    }

    /// <summary>
    /// The scenario a bug report's Reproduce cut, redacted as it may leave the host for a public issue
    /// (docs/tabletop/feedback-plan.md, R5): written as <c>scenario.json</c> with its content, the session's ids replaced
    /// by the report's, the source's answers stopped where the player wrote, then checked against every id, key and name
    /// the report and its table hold. The file, or the answer that says why it is not offered.
    /// </summary>
    public async Task<(StudioResponse? Refused, string? File)> RedactedAsync(Feedback report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Reproduction is not { } id || await store.GetAsync(id) is not { } document)
        {
            return (Error(FeedbackErrors.NotReproduced.Code, FeedbackErrors.NotReproduced.Message, 409), null);
        }

        // A scenario cut from a table still played, as a practice's may be, holds what its seats keep face down there.
        foreach (var source in new[] { document.Session, document.Scenario.Source.Run }.OfType<string>().Distinct(StringComparer.Ordinal))
        {
            if (TableKeys.IsTableId(source) && await stored.RecordAsync(source) is { Status: TableRecord.Open, Void: false })
            {
                return (Error(FeedbackErrors.UnderWay.Code, FeedbackErrors.UnderWay.Message, 409), null);
            }
        }

        if ((document.Schema ?? await SchemaOfAsync(document.Content)) is not { } schema)
        {
            return (Error("Scenario.ContentGone", $"The content this scenario was cut on ({document.Content}) is not kept at this host: the file could not carry it.", 409), null);
        }

        // A practice's report copied its entries up to when the player wrote; a table's journal went on after it.
        var context = report.Context;
        var scenario = RedactedScenario.Of(document.Scenario, report.Id, context?.Table is null ? null : context.JournalLength);
        var file = ScenarioFile.Write(new ScenarioDocument(scenario, document.Seat, document.Content, report.Id, document.Engine, document.Title, document.Goal, schema));
        var redacted = RedactedScenario.Check(file, report.Id, await NamesAsync(report, document));
        return redacted.IsFailure ? (Error(redacted.Error.Code, redacted.Error.Message, 409), null) : (null, file);
    }

    /// <summary>The content kept under a hash, as a scenario file carries it; none when this host keeps no such content.</summary>
    private async Task<JsonElement?> SchemaOfAsync(string version)
    {
        var (content, _) = await composer.PlayedOnAsync(version);
        return string.Equals(content.Version, version, StringComparison.Ordinal) && content.Services.GetService<GameContentFile>() is { } file
            ? await ScenarioContent.CarriedAsync(file.SchemaPath)
            : null;
    }

    /// <summary>
    /// What a redacted scenario must not hold, each with what it is: the report's sender and account, the session's ids,
    /// and, for a report from a table, every key, code and account its seats hold, and their players' names.
    /// </summary>
    private async Task<List<(string What, string? Name)>> NamesAsync(Feedback report, ScenarioDocument document)
    {
        var accounts = new List<string?> { report.Account };
        var names = new List<(string What, string? Name)>
        {
            ("the table", report.Context?.Table),
            ("the session", document.Session),
            ("the session", document.Scenario.Source.Run),
            ("the session's match", document.Scenario.Source.MatchId.ToString()),
        };
        if (report.Context?.Table is { } table && await stored.RecordAsync(table) is { } record)
        {
            names.AddRange([("the session's match", record.Match), ("the pilot's key", record.PilotToken)]);
            foreach (var seat in record.Seats)
            {
                names.AddRange([("a seat's key", seat.Token), ("a seat's code", seat.Code), ("a seat's invitation", seat.Invitation)]);
                accounts.AddRange([seat.Account, seat.InvitedAccount, seat.DeclinedBy]);
            }
        }

        foreach (var account in accounts.OfType<string>().Distinct(StringComparer.Ordinal))
        {
            names.Add(("an account", account));
            if (composer.NameOf is { } nameOf && SharedKernel.Identifiers.AccountId.TryParse(account, out var id))
            {
                names.Add(("a player's name", await nameOf(id)));
            }
        }

        // A signed-in player's sender is their account, said above; a guest's is a hash of its seat's key.
        names.Add(("the report's sender", report.Sender));
        return names;
    }

    /// <summary>Whether a scenario is still kept under its id: the one a report's Reproduce cut may have been removed since.</summary>
    public async Task<bool> KeepsAsync(string id) => await store.GetAsync(id) is not null;

    /// <summary>Lets go of a scenario Reproduce cut and nobody keeps, as removing it from the tab does, with no Activity line.</summary>
    public async Task ForgetAsync(string id) => await store.DeleteAsync(id);

    /// <summary>Where a scenario is played: the link a signed-in player, or the operator, opens.</summary>
    public static string Link(string id) => $"/battle?scenario={id}";

    /// <summary>A seat as a URL names it, <c>player1</c> or <c>player2</c>; none for any other word.</summary>
    private static PlayerSlot? SlotOf(string? seat) => seat switch
    {
        "player1" => PlayerSlot.Player1,
        "player2" => PlayerSlot.Player2,
        _ => null,
    };

    private static StudioResponse Error(string error, string message, int status) =>
        StudioResponse.OfJson(new { error, message }, ArtifactJson.LineOptions, status);

    private sealed record Asked(string? Session, int Round, string? Seat, string? Title, string? Goal);
}

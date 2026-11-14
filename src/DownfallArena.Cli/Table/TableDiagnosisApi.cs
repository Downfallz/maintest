using DownfallArena.Application.Admin;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Work;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The operator's diagnosis of a table in progress (docs/tabletop/scenarios-plan.md, S5b), behind the admin panel's
/// gate. <c>GET /api/tables/&lt;id&gt;/diagnosis</c> is what anyone may see of it: its seats and who is asked, its journal's
/// length and last lines by kind and seat, the content and rules it is played on, and its bot turns' troubles.
/// <c>POST .../diagnosis/replay</c> rebuilds it from its record and journal, aside, and says whether it replays.
/// <c>POST .../diagnosis/hidden</c> shows what each seat holds face down, and is noted with the operator's name.
/// <c>POST .../diagnosis/turn</c> puts the table's dead letters back on the queue, or says once more that its bots
/// owe a turn. Voiding is the void sheet's (ADR 0176).
/// </summary>
internal sealed class TableDiagnosisApi(TableRegistry registry, TableComposer composer, StoredSessions stored)
{
    private const string Prefix = AdminApi.Prefix + "/";

    private const string Segment = "/diagnosis";

    /// <summary>How many of the journal's last lines the diagnosis shows.</summary>
    public const int Tail = 12;

    /// <summary>The bot turns' queues, on a host whose turns go through them.</summary>
    public TurnQueues? Turns { get; set; }

    /// <summary>What operators do from the panel (A6): a reveal and a turn owed are noted with the rest.</summary>
    public OperatorNotes Notes { get; set; } = OperatorNotes.None;

    public static bool Names(string path) =>
        path.StartsWith(Prefix, StringComparison.Ordinal) && path[Prefix.Length..].Split('/') is [{ Length: > 0 }, "diagnosis", ..];

    public async Task<StudioResponse> HandleAsync(string method, string path, string operatorName)
    {
        var parts = path[Prefix.Length..].Split('/');
        var id = parts[0];
        if (!TableKeys.IsTableId(id) || await stored.RecordAsync(id) is not { } record)
        {
            return Error("Diagnosis.NoTable", $"No table '{id}' at this host.", 404);
        }

        return (method, parts) switch
        {
            ("GET", [_, "diagnosis"]) => await DescribeAsync(record),
            ("POST", [_, "diagnosis", "replay"]) => await ReplayAsync(record),
            ("POST", [_, "diagnosis", "hidden"]) => await HiddenAsync(record, operatorName),
            ("POST", [_, "diagnosis", "turn"]) => await TurnAsync(record, operatorName),
            _ => Error("Diagnosis.NoRoute", $"No such route: {method} {path}", 404),
        };
    }

    private async Task<StudioResponse> DescribeAsync(TableRecord record)
    {
        // An open table is picked up as any request naming it would; a closed one is read from its record alone.
        var table = record.Status == TableRecord.Open ? (await registry.FindAsync(record.Id)).Table : null;
        var journal = await composer.JournalAsync(record.Id);
        var entries = journal?.Entries ?? [];
        var seats = new List<object>();
        foreach (var slot in new[] { PlayerSlot.Player1, PlayerSlot.Player2 })
        {
            seats.Add(await SeatAsync(record, table, slot));
        }

        var letters = Turns is null ? [] : (await Turns.LettersAsync()).Where(one => one.Table == record.Id).ToList();
        var steppedSession = table?.Session as SteppedSession;
        return StudioResponse.OfJson(new
        {
            id = record.Id,
            status = record.Void ? "void" : record.Status,
            held = table is not null,
            begun = record.Begun || table?.Session.HasBegun == true,
            over = table?.IsOver,
            createdAt = record.CreatedAt,
            begunAt = record.BegunAt ?? table?.Session.BegunAt,
            round = table is null ? null : await table.RoundAsync(),
            answerBy = table?.Api.AnswerBy is { } answerBy ? await answerBy() : null,
            seats,
            journal = new
            {
                entries = entries.Count,
                lastWritten = (await composer.LastMoveAsync(record.Id))?.At,
                tail = entries.TakeLast(Tail).Select(Line).ToArray(),
            },
            basis = new
            {
                content = record.Content,
                rules = record.Rules is { } rules ? $"{rules.TeamSize}v{rules.TeamSize}, {rules.EnergyPerRound} energy, {rules.RoundCap} rounds, {rules.CombatDeclaration}" : null,
                engine = EngineVersion.Current.ToString(),
                refusedUnder = record.RefusedUnder is { } refused ? $"{refused.Engine} · {refused.Content}" : null,
            },
            turns = new
            {
                queued = Turns is not null,
                lastFailure = steppedSession?.LastFailure,
                letters = letters.Count,
                lastError = letters.Select(one => one.Letter?.Error).LastOrDefault(error => error is not null),
            },
        }, ArtifactJson.LineOptions);
    }

    private static async Task<object> SeatAsync(TableRecord record, PlayedTable? table, PlayerSlot slot)
    {
        var name = TableSeat.NameOf(slot);
        var recorded = record.Seats.FirstOrDefault(seat => seat.Slot == name);
        var person = table?.Seats.FirstOrDefault(seat => seat.Slot == slot)?.Person is not null || recorded?.Person == true;
        object? asked = null;
        if (table is not null && await table.Session.SnapshotAsync(slot) is { IsSuccess: true } snapshot)
        {
            // A person's asking is the question put to them, none once they answered all of it and wait for the other
            // seat, whose options still name the step; a bot's is its options, which it owes an answer to.
            asked = snapshot.Value.Waiting is { } waiting
                ? new { kind = waiting.Kind.ToString(), creature = waiting.Creature?.Value }
                : !table.Session.PlayedByItsPerson(slot) && snapshot.Value.Options.Kind != PlayerOptionsKind.Waiting
                    ? new { kind = snapshot.Value.Options.Kind.ToString(), creature = (int?)null }
                    : null;
        }

        return new
        {
            slot = name,
            name = table?.Session.SeatedName(slot) ?? (recorded is null ? null : recorded.Person ? "a person" : "a bot"),
            kind = person ? "person" : "bot",
            playedByBot = table is not null && !table.Session.PlayedByItsPerson(slot),
            asked,
        };
    }

    /// <summary>A journal line as the diagnosis shows it: what kind of line and whose, never what it chose.</summary>
    private static object Line(IJournalEntry entry) => entry switch
    {
        DecisionEntry decided => new { kind = decided.Decision.Decision.Kind.ToString(), seat = TableSeat.NameOf(decided.Decision.Slot) },
        SeatChangeEntry changed => new { kind = "SeatChange", seat = TableSeat.NameOf(changed.Slot) },
        ConcessionEntry conceded => new { kind = "Concession", seat = TableSeat.NameOf(conceded.Slot) },
        TimeoutEntry timedOut => new { kind = "Timeout", seat = string.Join(", ", timedOut.Late.Select(TableSeat.NameOf)) },
        _ => new { kind = entry.GetType().Name, seat = string.Empty },
    };

    private async Task<StudioResponse> ReplayAsync(TableRecord record)
    {
        var (rebuilt, refused) = await RebuildAsync(record);
        return rebuilt is { } match
            ? StudioResponse.OfJson(new { replays = true, entries = (int?)match.Entries, round = match.Player1.RoundNumber, message = $"The journal's {match.Entries} entries replay on the content and rules the table began on." }, ArtifactJson.LineOptions)
            : StudioResponse.OfJson(new { replays = false, entries = (int?)null, round = (int?)null, message = refused! }, ArtifactJson.LineOptions);
    }

    private async Task<StudioResponse> HiddenAsync(TableRecord record, string operatorName)
    {
        var (rebuilt, refused) = await RebuildAsync(record);
        if (rebuilt is not { } match)
        {
            return Error("Diagnosis.NoReplay", $"The table does not rebuild, so what its seats hold cannot be read: {refused}", 409);
        }

        Console.WriteLine($"  Table {record.Id}: what its seats hold face down was shown to {operatorName}.");
        await Notes.NoteAsync(operatorName, OperatorActions.RevealHidden, record.Id);
        return StudioResponse.OfJson(new
        {
            seats = new[]
            {
                Hidden(PlayerSlot.Player1, match.Player1, match.Held.Player1),
                Hidden(PlayerSlot.Player2, match.Player2, match.Held.Player2),
            },
        }, ArtifactJson.LineOptions);
    }

    private static object Hidden(PlayerSlot slot, PlayerBoardState board, IReadOnlyList<PlayerDecision> held) => new
    {
        slot = TableSeat.NameOf(slot),
        speeds = board.SpeedChoices.Select(one => new { creature = one.Creature.Value, speed = one.Speed.ToString() }).ToArray(),
        intents = board.Intents.Select(one => new { creature = one.Actor.Value, spell = one.Spell.ToString() }).ToArray(),
        picks = board.EvolutionChoices.Select(one => new { creature = one.Creature.Value, tier = one.Tier.ToString() }).ToArray(),
        held = held.Select(one => new { kind = one.Kind.ToString(), creature = one.Creature?.Value, spell = one.Spell?.ToString(), tier = one.Tier?.ToString(), speed = one.Speed?.ToString(), pass = one.IsPass }).ToArray(),
    };

    private async Task<StudioResponse> TurnAsync(TableRecord record, string operatorName)
    {
        if (Turns is not null)
        {
            var letters = (await Turns.LettersAsync()).Where(one => one.Table == record.Id).Select(one => one.Id).ToList();
            if (letters.Count > 0)
            {
                var handled = await Turns.RequeueAsync(letters);
                await Notes.NoteAsync(operatorName, OperatorActions.OweTurn, record.Id, $"{letters.Count} dead letter(s) requeued");
                return StudioResponse.OfJson(new { message = $"{letters.Count} of this table's dead letters went back on the queue.", handled }, ArtifactJson.LineOptions);
            }
        }

        var table = record.Status == TableRecord.Open ? (await registry.FindAsync(record.Id)).Table : null;
        if (table?.Session is not SteppedSession session || session.IsOver)
        {
            return Error("Diagnosis.NoTurn", "The table is not being played at this host, so no bot turn can be owed for it.", 409);
        }

        // Owing a turn nobody owes takes none: the session finds nothing to answer and stops. So a table with a bot in it
        // is always owed its turn again, which is the safe thing to do for a turn that may have been lost on its way.
        if (new[] { PlayerSlot.Player1, PlayerSlot.Player2 }.All(session.PlayedByItsPerson))
        {
            return Error("Diagnosis.NoBot", "Both seats are played by people: there is no bot turn to owe.", 409);
        }

        session.OweTurn();
        Console.WriteLine($"  Table {record.Id}: a bot turn owed again by {operatorName}.");
        await Notes.NoteAsync(operatorName, OperatorActions.OweTurn, record.Id, "bot turn owed again");
        return StudioResponse.OfJson(new { message = "The bots' turn is owed again: a bot that is asked answers, and one that is not takes nothing." }, ArtifactJson.LineOptions);
    }

    private async Task<(TableRebuild.Rebuilt? Rebuilt, string? Refused)> RebuildAsync(TableRecord record)
    {
        var (content, _) = await composer.PlayedOnAsync(record.Content);
        if (record.Content is { } version && !string.Equals(content.Version, version, StringComparison.Ordinal))
        {
            return (null, $"The content the table began on ({version}) is not kept at this host.");
        }

        var entries = (await composer.JournalAsync(record.Id))?.Entries ?? [];
        return await TableRebuild.AsideAsync(record, entries, content);
    }

    private static StudioResponse Error(string error, string message, int status) =>
        StudioResponse.OfJson(new { error, message }, ArtifactJson.LineOptions, status);
}

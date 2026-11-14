using System.Text;
using System.Text.Json;
using DownfallArena.Application.Admin;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Admin;
using DownfallArena.Infrastructure.Learning;
using NSubstitute;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The operator's diagnosis of a table in progress (docs/tabletop/scenarios-plan.md, S5b): what anyone may see of it,
/// its journal replayed aside, what its seats hold face down on request and noted, and its bot turn owed again.
/// </summary>
public sealed class TableDiagnosisApiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"diagnosis-{Guid.CreateVersion7():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private static string Route(string table, string rest = "") => $"{AdminApi.Prefix}/{table}/diagnosis{rest}";

    /// <summary>A table of a person against a bot, the person at their seat and asked their first question, recorded.</summary>
    private static async Task<(TableRegistry Registry, PlayedTable Table)> PlayingAsync(HostedTables hosted)
    {
        var registry = hosted.Registry();
        var table = await hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        table.Seats[0].Person!.Arrive();
        table.Session.Begin();
        for (var waited = 0; waited < 100 && (await table.Session.SnapshotAsync(Domain.Matches.PlayerSlot.Player1)).Value.Waiting is null; waited++)
        {
            await Task.Delay(50, Cancel);
        }

        await table.RecordAsync(Cancel);
        return (registry, table);
    }

    [Fact]
    public async Task The_diagnosis_says_who_sits_who_is_asked_and_how_far_the_journal_goes_and_never_what_was_chosen()
    {
        using var hosted = new HostedTables(recording: true);
        var (registry, table) = await PlayingAsync(hosted);
        using var _ = registry;
        var api = new TableDiagnosisApi(registry, hosted.Composer, hosted.Stored!);

        var diagnosis = Read(await api.HandleAsync("GET", Route(table.Id), "mark"));

        diagnosis.GetProperty("held").GetBoolean().ShouldBeTrue();
        diagnosis.GetProperty("status").GetString().ShouldBe("open");
        var seats = diagnosis.GetProperty("seats");
        seats[0].GetProperty("kind").GetString().ShouldBe("person");
        seats[0].GetProperty("asked").GetProperty("kind").GetString().ShouldNotBeNullOrEmpty();
        seats[1].GetProperty("kind").GetString().ShouldBe("bot");
        var journal = diagnosis.GetProperty("journal");
        journal.GetProperty("entries").GetInt32().ShouldBe(journal.GetProperty("tail").GetArrayLength());
        foreach (var line in journal.GetProperty("tail").EnumerateArray())
        {
            line.EnumerateObject().Select(one => one.Name).ShouldBe(["kind", "seat"]);
        }
    }

    [Fact]
    public async Task A_table_whose_journal_replays_says_so_and_the_table_being_played_is_untouched()
    {
        using var hosted = new HostedTables(recording: true);
        var (registry, table) = await PlayingAsync(hosted);
        using var _ = registry;
        var api = new TableDiagnosisApi(registry, hosted.Composer, hosted.Stored!);
        var before = await table.Session.SnapshotAsync(Domain.Matches.PlayerSlot.Player1);

        var replay = Read(await api.HandleAsync("POST", Route(table.Id, "/replay"), "mark"));

        replay.GetProperty("replays").GetBoolean().ShouldBeTrue(replay.GetProperty("message").GetString());
        (await table.Session.SnapshotAsync(Domain.Matches.PlayerSlot.Player1)).Value.Waiting.ShouldBe(before.Value.Waiting);
    }

    [Fact]
    public async Task What_the_seats_hold_face_down_is_shown_on_request_and_noted_with_the_operator_s_name()
    {
        using var hosted = new HostedTables(recording: true);
        var (registry, table) = await PlayingAsync(hosted);
        using var _ = registry;
        var actions = new OperatorActions(new FileOperatorLog(_root), TimeProvider.System);
        var api = new TableDiagnosisApi(registry, hosted.Composer, hosted.Stored!) { Notes = new OperatorNotes(actions) };

        var hidden = Read(await api.HandleAsync("POST", Route(table.Id, "/hidden"), "mark"));

        hidden.GetProperty("seats").GetArrayLength().ShouldBe(2);
        var noted = (await actions.PageAsync(1, Cancel)).Actions.ShouldHaveSingleItem();
        (noted.Action, noted.By, noted.Target).ShouldBe((OperatorActions.RevealHidden, "mark", table.Id));
    }

    [Fact]
    public async Task A_table_of_two_people_has_no_bot_turn_to_owe()
    {
        using var hosted = new HostedTables(recording: true);
        using var registry = hosted.Registry();
        var table = await hosted.Composer.ComposeAsync(HostedTables.TwoPeople, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        await table.RecordAsync(Cancel);
        var api = new TableDiagnosisApi(registry, hosted.Composer, hosted.Stored!);

        var refused = await api.HandleAsync("POST", Route(table.Id, "/turn"), "mark");

        refused.Status.ShouldBe(409);
        Read(refused).GetProperty("error").GetString().ShouldBe("Diagnosis.NoBot");
    }

    /// <summary>A bot's turn lost on its way (a queue nobody took it from): owing it again hands it to whoever takes them.</summary>
    [Fact]
    public async Task A_lost_bot_turn_is_owed_again_and_noted()
    {
        var turns = Substitute.For<IAgentTurns>();
        using var hosted = new HostedTables(directory => new FileArtifactStore(directory), turns: turns);
        var registry = hosted.Registry();
        using var _ = registry;
        var table = await hosted.Composer.ComposeAsync(HostedTables.Bots, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        table.Session.Begin();
        await table.RecordAsync(Cancel);
        var actions = new OperatorActions(new FileOperatorLog(_root), TimeProvider.System);
        var api = new TableDiagnosisApi(registry, hosted.Composer, hosted.Stored!) { Notes = new OperatorNotes(actions) };
        turns.ClearReceivedCalls();

        var owed = await api.HandleAsync("POST", Route(table.Id, "/turn"), "mark");

        owed.Status.ShouldBe(200, Encoding.UTF8.GetString(owed.Body));
        turns.ReceivedCalls().ShouldNotBeEmpty();
        (await actions.PageAsync(1, Cancel)).Actions.ShouldHaveSingleItem().Action.ShouldBe(OperatorActions.OweTurn);
    }

    [Fact]
    public async Task A_table_the_host_never_recorded_has_no_diagnosis()
    {
        using var hosted = new HostedTables(recording: true);
        using var registry = hosted.Registry();
        var api = new TableDiagnosisApi(registry, hosted.Composer, hosted.Stored!);

        (await api.HandleAsync("GET", Route("20261114-000000-deadbeef"), "mark")).Status.ShouldBe(404);
    }
}

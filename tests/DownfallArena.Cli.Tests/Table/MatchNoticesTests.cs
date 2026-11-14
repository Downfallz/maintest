using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Agents;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// How a match ended reaches each seat's account (ADR 0129, stage 4): a victory, a defeat or a draw against the
/// other side by name; nothing to a bot's seat; one notice to the one account of a shared device.
/// </summary>
public sealed class MatchNoticesTests : IDisposable
{
    private const string Key = InMemoryDeviceStore.Key;
    private const string Auth = InMemoryDeviceStore.Auth;

    private readonly HostedTables _hosted = new(recording: true);
    private readonly RecordedPush _sender = new();
    private readonly AccountDevices _devices = new(new InMemoryDeviceStore(), TimeProvider.System);
    private readonly Dictionary<AccountId, string> _names = [];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>Both seats persons played to the end by their understudies: a two-person match that finishes on its own.</summary>
    private static TableRequest TwoPeople => new(null, null, Who: null, Handover: 999, Seed: 7);

    private static TableRequest OnePerson => new(null, AgentSpec.Parse("greedy"), Who: null, Handover: 999, Seed: 7);

    private TableComposer Composer() => new(_hosted.Services, HostedTables.Rules, new FileArtifactStore(_hosted.RunsDirectory))
    {
        Notices = new AccountNotices(_sender, _devices),
        NameOf = id => Task.FromResult(_names.GetValueOrDefault(id)),
        Agents = [new SeatableAgent("greedy", "Greedy — the plain one", Featured: true)],
        Index = _hosted.Index,
    };

    private async Task<AccountId> PlayerAsync(string name)
    {
        var id = AccountId.New();
        _names[id] = name;
        await _devices.AddAsync(id, new PushDevice($"https://push.example/{name}", Key, Auth, default), Cancel);
        return id;
    }

    private static async Task FinishedAsync(PlayedTable table)
    {
        foreach (var seat in table.Seats.Where(seat => seat.Person is not null))
        {
            seat.Person!.Arrive();
        }

        table.Session.Begin();
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(60), Cancel);
        for (var attempt = 0; attempt < 500 && !table.IsFinished; attempt++)
        {
            await Task.Delay(20, Cancel);
        }
    }

    private (string Title, string Body) NoticeTo(string name)
    {
        var sent = _sender.Sent.Single(sent => sent.Endpoint == $"https://push.example/{name}");
        var notice = JsonDocument.Parse(sent.Payload).RootElement;
        return (notice.GetProperty("title").GetString()!, notice.GetProperty("body").GetString()!);
    }

    [Fact]
    public async Task Each_player_is_told_how_the_match_ended_against_the_other_by_name()
    {
        var table = await Composer().ComposeAsync(TwoPeople, Cancel);
        table.Seats[0].Account = await PlayerAsync("MarkF");
        table.Seats[1].Account = await PlayerAsync("bastien");

        await FinishedAsync(table);

        var outcome = (await table.Session.Outcome).Value;
        var mark = NoticeTo("MarkF");
        var bastien = NoticeTo("bastien");
        if (outcome.Winner is null)
        {
            mark.ShouldBe(("A draw", "A draw against bastien."));
            bastien.ShouldBe(("A draw", "A draw against MarkF."));
        }
        else
        {
            var markWon = outcome.Winner == Domain.Matches.PlayerSlot.Player1;
            mark.ShouldBe(markWon ? ("Victory", "You won against bastien.") : ("Defeat", "bastien won."));
            bastien.ShouldBe(markWon ? ("Defeat", "MarkF won.") : ("Victory", "You won against MarkF."));
        }

        _sender.Sent.ShouldAllBe(sent => sent.Topic == "match");
        JsonDocument.Parse(_sender.Sent[0].Payload).RootElement.GetProperty("url").GetString().ShouldBe($"/play?table={table.Id}");
        table.Dispose();
    }

    [Fact]
    public async Task A_bot_s_seat_is_told_nothing_and_the_person_is_told_against_the_bot_by_its_level()
    {
        var table = await Composer().ComposeAsync(OnePerson, Cancel);
        table.Seats[0].Account = await PlayerAsync("MarkF");

        await FinishedAsync(table);

        _sender.Sent.ShouldHaveSingleItem();
        // A player knows a bot by its level, never its name (ADR 0177): Greedy is Medium.
        NoticeTo("MarkF").Body.ShouldMatch("Medium bot");
        NoticeTo("MarkF").Body.ShouldNotContain("Greedy");
        table.Dispose();
    }

    [Fact]
    public async Task A_shared_device_holding_both_seats_is_told_once_in_the_room_s_words()
    {
        var table = await Composer().ComposeAsync(TwoPeople, Cancel);
        var mark = await PlayerAsync("MarkF");
        table.Seats[0].Account = mark;
        table.Seats[1].Account = mark;

        await FinishedAsync(table);

        var (title, body) = NoticeTo("MarkF");
        title.ShouldBe("Match over");
        body.ShouldStartWith("The match is over: ");
        _sender.Sent.Count.ShouldBe(1);
        table.Dispose();
    }

    [Fact]
    public async Task A_notice_that_cannot_be_sent_does_not_keep_the_recording_from_finishing()
    {
        using var registry = _hosted.Registry();
        var composer = Composer();
        composer.NameOf = _ => throw new IOException("The account store went away.");
        var table = await composer.ComposeAsync(TwoPeople, Cancel);
        table.Seats[0].Account = await PlayerAsync("MarkF");
        table.Seats[1].Account = await PlayerAsync("bastien");
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        await table.RecordAsync(Cancel);

        await FinishedAsync(table);

        (await _hosted.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Finished, "the files are written before anybody is told");
        _sender.Sent.ShouldBeEmpty("the names could not be read, so no notice went out, and nothing else broke");
        await registry.RemoveAsync(table.Id, Cancel);
    }

    [Fact]
    public async Task A_finished_table_nobody_with_an_account_played_tells_nobody()
    {
        var table = await Composer().ComposeAsync(OnePerson, Cancel);

        await FinishedAsync(table);

        _sender.Sent.ShouldBeEmpty();
        table.Dispose();
    }

    public void Dispose() => _hosted.Dispose();
}

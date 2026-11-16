using System.Text;
using System.Text.Json;
using DownfallArena.Application.Admin;
using DownfallArena.Application.Admin.Ports;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Work;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>The bots' turn queues in the operator's Jobs tab (admin plan, A3b): seen, requeued, dropped.</summary>
public sealed class TurnsApiTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);

    private readonly InMemoryWorkChannel _turns = new(AgentTurnWorker.Queue, TimeProvider.System);
    private readonly InMemoryWorkChannel _poison = new(PoisonedWork.QueueOf(AgentTurnWorker.Queue), TimeProvider.System);
    private readonly InMemoryWorkChannel _letters = new(DeadLetter.QueueOf(AgentTurnWorker.Queue), TimeProvider.System);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private TurnsApi Api() => new(new TurnQueues(_turns, _poison, _letters));

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private async Task LetterAsync(string table, string error = "The journal could not be read.\n   at Somewhere()")
    {
        var letter = new DeadLetter(AgentTurnWorker.Queue, new OwedTurn(table).ToJson(), error, 5, Start, Start.AddMinutes(1), Start.AddMinutes(2), "worker-1");
        await _letters.SendAsync(letter.ToJson(), Timeout.InfiniteTimeSpan, cancellationToken: Cancel);
    }

    [Fact]
    public async Task The_queues_and_the_dead_letters_are_listed_with_each_letter_s_table_and_the_first_line_of_its_error()
    {
        await _turns.SendAsync(new OwedTurn("waiting").ToJson(), cancellationToken: Cancel);
        await LetterAsync("table-7");

        var listed = Json(await Api().HandleAsync("GET", TurnsApi.Prefix, "", "op"));

        listed.GetProperty("queues").EnumerateArray().Select(queue => (queue.GetProperty("name").GetString(), queue.GetProperty("count").GetInt32()))
            .ShouldBe([(AgentTurnWorker.Queue, 1), (_poison.Name, 0), (_letters.Name, 1)]);
        var letter = listed.GetProperty("letters").EnumerateArray().ShouldHaveSingleItem();
        letter.GetProperty("table").GetString().ShouldBe("table-7");
        letter.GetProperty("error").GetString().ShouldBe("The journal could not be read.");
        letter.GetProperty("fullError").GetString().ShouldNotBeNull().ShouldContain("at Somewhere()");
        letter.GetProperty("delivered").GetInt64().ShouldBe(5);
    }

    [Fact]
    public async Task A_letter_requeued_by_its_id_puts_its_turn_back_and_leaves_the_others()
    {
        await LetterAsync("table-1");
        await LetterAsync("table-2");
        var id = (await _letters.PeekAsync(10, Cancel))[0].Id;

        var answer = Json(await Api().HandleAsync("POST", $"{TurnsApi.Prefix}/requeue", JsonSerializer.Serialize(new { ids = new[] { id } }), "op"));

        answer.GetProperty("handled").EnumerateArray().Select(item => item.GetString()).ShouldBe([id]);
        _turns.Bodies.ShouldBe([new OwedTurn("table-1").ToJson()]);
        (await _letters.CountAsync(Cancel)).ShouldBe(1);
    }

    [Fact]
    public async Task Every_letter_is_requeued_when_all_are_asked_for()
    {
        await LetterAsync("table-1");
        await LetterAsync("table-2");

        await Api().HandleAsync("POST", $"{TurnsApi.Prefix}/requeue", """{"all":true}""", "op");

        _turns.Bodies.Count.ShouldBe(2);
        (await _letters.CountAsync(Cancel)).ShouldBe(0);
    }

    [Fact]
    public async Task A_dropped_letter_is_gone_and_its_turn_is_not_requeued()
    {
        await LetterAsync("table-1");
        var id = (await _letters.PeekAsync(10, Cancel))[0].Id;

        await Api().HandleAsync("POST", $"{TurnsApi.Prefix}/drop", JsonSerializer.Serialize(new { ids = new[] { id } }), "op");

        _turns.Bodies.ShouldBeEmpty();
        (await _letters.CountAsync(Cancel)).ShouldBe(0);
    }

    /// <summary>A dropped letter is gone from its queue, so the operator log (A6) keeps which it was, by its id.</summary>
    [Fact]
    public async Task A_drop_is_noted_with_the_ids_of_the_letters_it_dropped()
    {
        await LetterAsync("table-1");
        var id = (await _letters.PeekAsync(10, Cancel))[0].Id;
        var log = new Kept();
        var api = Api();
        api.Notes = new OperatorNotes(new OperatorActions(log, TimeProvider.System));

        await api.HandleAsync("POST", $"{TurnsApi.Prefix}/drop", JsonSerializer.Serialize(new { ids = new[] { id } }), "op");

        var noted = log.All.ShouldHaveSingleItem();
        noted.Action.ShouldBe(OperatorActions.Drop);
        noted.Detail.ShouldBe($"1 letter: {id}");
    }

    [Theory]
    [InlineData("requeue", "{}")]
    [InlineData("drop", """{"all":true}""")]
    [InlineData("drop", "")]
    public async Task A_requeue_or_a_drop_that_names_no_letter_is_refused(string action, string body)
    {
        (await Api().HandleAsync("POST", $"{TurnsApi.Prefix}/{action}", body, "op")).Status.ShouldBe(400);
    }

    [Fact]
    public void The_first_line_of_an_error_is_what_a_list_shows()
    {
        TurnsApi.FirstLine("first\r\nsecond").ShouldBe("first");
        TurnsApi.FirstLine(null).ShouldBe(string.Empty);
    }

    private sealed class Kept : IOperatorLog
    {
        private readonly List<OperatorAction> _kept = [];

        public IReadOnlyList<OperatorAction> All => _kept;

        public Task RecordAsync(OperatorAction action, CancellationToken cancellationToken = default)
        {
            _kept.Add(action);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([.. _kept.Select(action => action.Id)]);

        public Task<OperatorAction?> FindAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_kept.FirstOrDefault(action => action.Id == id));
    }
}

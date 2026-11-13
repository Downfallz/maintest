using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Work;
using DownfallArena.Infrastructure.Work;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Infrastructure.Tests.Work;

/// <summary>The bots' turn queues as the operator sees and handles them (admin plan, A3b).</summary>
public sealed class TurnQueuesTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Start);
    private readonly InMemoryWorkChannel _turns;
    private readonly InMemoryWorkChannel _poison;
    private readonly InMemoryWorkChannel _letters;

    public TurnQueuesTests()
    {
        _turns = new InMemoryWorkChannel(AgentTurnWorker.Queue, _clock);
        _poison = new InMemoryWorkChannel(PoisonedWork.QueueOf(AgentTurnWorker.Queue), _clock);
        _letters = new InMemoryWorkChannel(DeadLetter.QueueOf(AgentTurnWorker.Queue), _clock);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private TurnQueues Queues() => new(_turns, _poison, _letters);

    private async Task LetterAsync(string table, string error = "The journal could not be read.")
    {
        var letter = new DeadLetter(AgentTurnWorker.Queue, new OwedTurn(table).ToJson(), error, 5, Start, Start.AddMinutes(1), Start.AddMinutes(2), "worker-1");
        await _letters.SendAsync(letter.ToJson(), Timeout.InfiniteTimeSpan, cancellationToken: Cancel);
    }

    private async Task<IReadOnlyList<string>> IdsAsync() => [.. (await _letters.PeekAsync(100, Cancel)).Select(item => item.Id)];

    [Fact]
    public async Task Each_queue_says_how_many_items_it_holds_and_when_its_oldest_was_put_on_it()
    {
        await _turns.SendAsync(new OwedTurn("t1").ToJson(), cancellationToken: Cancel);
        _clock.Advance(TimeSpan.FromMinutes(3));
        await _turns.SendAsync(new OwedTurn("t2").ToJson(), cancellationToken: Cancel);
        await LetterAsync("t3");

        var states = await Queues().StatesAsync(Cancel);

        states.ShouldBe([
            new QueueState(AgentTurnWorker.Queue, 2, Start),
            new QueueState(_poison.Name, 0, null),
            new QueueState(_letters.Name, 1, Start.AddMinutes(3)),
        ]);
    }

    [Fact]
    public async Task A_dead_letter_is_listed_with_the_table_it_owed_a_turn_at()
    {
        await LetterAsync("table-7", "The store is out.");

        var listed = (await Queues().LettersAsync(Cancel)).ShouldHaveSingleItem();

        listed.Table.ShouldBe("table-7");
        listed.Letter.ShouldNotBeNull().Error.ShouldBe("The store is out.");
    }

    [Fact]
    public async Task Requeueing_a_letter_puts_its_turn_back_as_a_new_item_and_deletes_it()
    {
        await LetterAsync("table-1");
        await LetterAsync("table-2");
        var first = (await IdsAsync())[0];

        var handled = await Queues().RequeueAsync([first], Cancel);

        handled.Handled.ShouldBe([first]);
        _turns.Bodies.ShouldBe([new OwedTurn("table-1").ToJson()]);
        (await _letters.CountAsync(Cancel)).ShouldBe(1);
        (await _letters.PeekAsync(10, Cancel)).ShouldHaveSingleItem("the letter not asked for is let back at once").Body.ShouldContain("table-2");
        (await _turns.ReceiveAsync(1, TimeSpan.FromMinutes(1), Cancel)).ShouldHaveSingleItem().Delivered.ShouldBe(1);
    }

    [Fact]
    public async Task Requeueing_every_letter_empties_the_dead_letters()
    {
        await LetterAsync("table-1");
        await LetterAsync("table-2");

        var handled = await Queues().RequeueAsync(null, Cancel);

        handled.Handled.Count.ShouldBe(2);
        _turns.Bodies.Count.ShouldBe(2);
        (await _letters.CountAsync(Cancel)).ShouldBe(0);
    }

    [Fact]
    public async Task Dropping_a_letter_deletes_it_and_requeues_nothing()
    {
        await LetterAsync("table-1");
        var id = (await IdsAsync()).ShouldHaveSingleItem();

        var handled = await Queues().DropAsync([id], Cancel);

        handled.Handled.ShouldBe([id]);
        _turns.Bodies.ShouldBeEmpty();
        (await _letters.CountAsync(Cancel)).ShouldBe(0);
    }

    [Fact]
    public async Task A_letter_asked_for_that_is_gone_is_said_missing_and_the_others_stay()
    {
        await LetterAsync("table-1");

        var handled = await Queues().DropAsync(["gone"], Cancel);

        handled.Handled.ShouldBeEmpty();
        handled.Missing.ShouldBe(["gone"]);
        (await _letters.PeekAsync(10, Cancel)).Count.ShouldBe(1);
    }
}

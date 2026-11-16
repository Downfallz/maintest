using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;
using DownfallArena.Infrastructure.Work;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Infrastructure.Tests.Work;

/// <summary>
/// The worker that takes the turns a queue says tables owe (ADR 0159, phase 5): until nothing more is owed, again
/// later when a turn failed, and onto the poison queue with its error at its fifth failed delivery, as a queue
/// trigger does. The second worker keeps what the poison queue holds for good, on the dead-letter queue.
/// </summary>
public sealed class AgentTurnWorkerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryWorkChannels _channels;
    private readonly List<string> _said = [];

    public AgentTurnWorkerTests()
    {
        _channels = new InMemoryWorkChannels(_clock);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_table_s_turns_are_taken_until_nothing_more_is_owed_and_its_item_is_done_with()
    {
        var table = new Scripted("table-1", AgentTurn.Taken, AgentTurn.Taken, AgentTurn.Done);
        var worker = await WorkerAsync(table);
        await OweAsync("table-1");

        await worker.RunOnceAsync(Cancel);

        table.Taken.ShouldBe(3);
        (await HeldAsync(AgentTurnWorker.Queue)).ShouldBeEmpty();
    }

    [Fact]
    public async Task One_item_is_taken_at_a_look_and_the_others_wait_unreceived()
    {
        var table = new Scripted("table-1", AgentTurn.Done);
        var worker = await WorkerAsync(table);
        await OweAsync("table-1");
        await OweAsync("table-1");

        var taken = await worker.RunOnceAsync(Cancel);
        var waiting = await (await TurnsAsync()).ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel);

        taken.ShouldBe(1);
        waiting.ShouldHaveSingleItem().Delivered.ShouldBe(1);
    }

    [Fact]
    public async Task The_time_an_item_is_hidden_for_is_counted_from_when_it_was_received()
    {
        var table = new Scripted("table-1", AgentTurn.Taken, AgentTurn.Taken, AgentTurn.Done);
        var turns = await TurnsAsync();
        var worker = new AgentTurnWorker(
            turns,
            await PoisonAsync(),
            (_, _) =>
            {
                // Finding the table took most of the time the item is hidden for.
                _clock.Advance(AgentTurnWorker.HiddenFor / 2 + TimeSpan.FromSeconds(1));
                return Task.FromResult<IAgentTurnTaker?>(table);
            },
            _clock);
        await OweAsync("table-1");

        await worker.RunOnceAsync(Cancel);

        table.Taken.ShouldBe(1);
        (await turns.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel)).ShouldHaveSingleItem().Delivered.ShouldBe(1);
    }

    [Fact]
    public async Task An_item_for_a_table_nobody_can_take_turns_at_is_done_with()
    {
        var worker = await WorkerAsync(table: null);
        await OweAsync("gone");

        await worker.RunOnceAsync(Cancel);

        (await HeldAsync(AgentTurnWorker.Queue)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_turn_comes_back_later_delivered_once_more()
    {
        var table = new Scripted("table-1", AgentTurn.Failed, AgentTurn.Done);
        var worker = await WorkerAsync(table);
        await OweAsync("table-1");
        await worker.RunOnceAsync(Cancel);
        var turns = await TurnsAsync();

        var early = await turns.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel);
        _clock.Advance(AgentTurnWorker.FirstRetry);
        var later = await turns.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel);

        early.ShouldBeEmpty();
        later.ShouldHaveSingleItem().Delivered.ShouldBe(2);
    }

    [Fact]
    public async Task An_item_whose_turn_fails_at_every_delivery_goes_on_the_poison_queue_with_its_error_at_the_fifth()
    {
        var table = new Scripted("table-1", AgentTurn.Failed) { LastFailure = "IOException: The store is out." };
        var worker = await WorkerAsync(table);
        await OweAsync("table-1");

        for (var delivery = 1; delivery <= AgentTurnWorker.MostDeliveries; delivery++)
        {
            (await worker.RunOnceAsync(Cancel)).ShouldBe(1);
            _clock.Advance(TimeSpan.FromMinutes(2));
        }

        (await HeldAsync(AgentTurnWorker.Queue)).ShouldBeEmpty();
        var poisoned = PoisonedWork.Parse((await HeldAsync(PoisonedWork.QueueOf(AgentTurnWorker.Queue))).ShouldHaveSingleItem()).ShouldNotBeNull();
        poisoned.Queue.ShouldBe(AgentTurnWorker.Queue);
        OwedTurn.Parse(poisoned.Body).ShouldBe(new OwedTurn("table-1"));
        poisoned.Error.ShouldBe("IOException: The store is out.");
        poisoned.Delivered.ShouldBe(AgentTurnWorker.MostDeliveries);
        table.Taken.ShouldBe(AgentTurnWorker.MostDeliveries);
    }

    [Fact]
    public async Task A_table_that_cannot_be_found_for_a_failure_is_poisoned_with_the_failure_s_kind_and_words()
    {
        var turns = await TurnsAsync();
        var worker = new AgentTurnWorker(turns, await PoisonAsync(), (_, _) => throw new InvalidDataException("The record is not one."), _clock);
        await OweAsync("table-1");

        for (var delivery = 1; delivery <= AgentTurnWorker.MostDeliveries; delivery++)
        {
            await worker.RunOnceAsync(Cancel);
            _clock.Advance(TimeSpan.FromMinutes(2));
        }

        PoisonedWork.Parse((await HeldAsync(PoisonedWork.QueueOf(AgentTurnWorker.Queue))).ShouldHaveSingleItem()).ShouldNotBeNull()
            .Error.ShouldBe("InvalidDataException: The record is not one.");
    }

    [Fact]
    public async Task A_busy_table_is_handed_on_as_a_new_item_rather_than_counted_as_a_failure()
    {
        var table = new Scripted("table-1", AgentTurn.Moved);
        var worker = await WorkerAsync(table);
        await OweAsync("table-1");
        await worker.RunOnceAsync(Cancel);
        var turns = await TurnsAsync();

        _clock.Advance(AgentTurnWorker.BusyFor);
        var again = await turns.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel);

        table.Taken.ShouldBe(AgentTurnWorker.MovedInARow);
        again.ShouldHaveSingleItem().Delivered.ShouldBe(1);
    }

    [Fact]
    public async Task An_item_that_is_not_a_turn_is_poisoned_at_once()
    {
        var worker = await WorkerAsync(table: null);
        await (await TurnsAsync()).SendAsync("not json", cancellationToken: Cancel);

        await worker.RunOnceAsync(Cancel);

        (await HeldAsync(AgentTurnWorker.Queue)).ShouldBeEmpty();
        PoisonedWork.Parse((await HeldAsync(PoisonedWork.QueueOf(AgentTurnWorker.Queue))).ShouldHaveSingleItem()).ShouldNotBeNull().Body.ShouldBe("not json");
    }

    [Fact]
    public async Task The_second_worker_keeps_a_poisoned_item_for_good_on_the_dead_letter_queue_with_its_error()
    {
        var table = new Scripted("table-1", AgentTurn.Failed) { LastFailure = "IOException: The store is out." };
        var worker = await WorkerAsync(table);
        var keeper = new DeadLetterWorker(await PoisonAsync(), await DeadLettersAsync(), _clock, "replica-7") { Said = _said.Add };
        await OweAsync("table-1");
        for (var delivery = 1; delivery <= AgentTurnWorker.MostDeliveries; delivery++)
        {
            await worker.RunOnceAsync(Cancel);
            _clock.Advance(TimeSpan.FromMinutes(2));
        }

        var kept = await keeper.RunOnceAsync(Cancel);

        kept.ShouldBe(1);
        (await HeldAsync(PoisonedWork.QueueOf(AgentTurnWorker.Queue))).ShouldBeEmpty();
        var letter = DeadLetter.Parse((await HeldAsync(DeadLetter.QueueOf(AgentTurnWorker.Queue))).ShouldHaveSingleItem()).ShouldNotBeNull();
        letter.Queue.ShouldBe(AgentTurnWorker.Queue);
        OwedTurn.Parse(letter.Body).ShouldBe(new OwedTurn("table-1"));
        letter.Error.ShouldBe("IOException: The store is out.");
        letter.Delivered.ShouldBe(AgentTurnWorker.MostDeliveries);
        letter.Host.ShouldBe("replica-7");
        letter.DeadLetteredAt.ShouldBe(_clock.GetUtcNow());
        letter.PoisonedAt.ShouldBeLessThan(letter.DeadLetteredAt);
        _said[^1].ShouldStartWith($"  Dead letter on {DeadLetter.QueueOf(AgentTurnWorker.Queue)}:");
        _said[^1].ShouldContain("IOException: The store is out.");
    }

    [Fact]
    public async Task An_item_put_on_the_poison_queue_by_something_else_is_kept_with_what_its_queue_knows()
    {
        var poison = await PoisonAsync();
        await poison.SendAsync("raw", cancellationToken: Cancel);
        var keeper = new DeadLetterWorker(poison, await DeadLettersAsync(), _clock, "replica-7");

        await keeper.RunOnceAsync(Cancel);

        var letter = DeadLetter.Parse((await HeldAsync(DeadLetter.QueueOf(AgentTurnWorker.Queue))).ShouldHaveSingleItem()).ShouldNotBeNull();
        letter.Queue.ShouldBe(PoisonedWork.QueueOf(AgentTurnWorker.Queue));
        letter.Body.ShouldBe("raw");
        letter.Delivered.ShouldBe(1);
    }

    private async Task<AgentTurnWorker> WorkerAsync(Scripted? table) =>
        new(await TurnsAsync(), await PoisonAsync(), (run, _) => Task.FromResult<IAgentTurnTaker?>(table is not null && table.Run == run ? table : null), _clock)
        {
            Said = _said.Add,
        };

    private Task<IWorkChannel> TurnsAsync() => _channels.OpenAsync(AgentTurnWorker.Queue, Cancel);

    private Task<IWorkChannel> PoisonAsync() => _channels.OpenAsync(PoisonedWork.QueueOf(AgentTurnWorker.Queue), Cancel);

    private Task<IWorkChannel> DeadLettersAsync() => _channels.OpenAsync(DeadLetter.QueueOf(AgentTurnWorker.Queue), Cancel);

    private async Task OweAsync(string run) => await (await TurnsAsync()).SendAsync(new OwedTurn(run).ToJson(), cancellationToken: Cancel);

    private async Task<IReadOnlyList<string>> HeldAsync(string queue) => ((InMemoryWorkChannel)await _channels.OpenAsync(queue, Cancel)).Bodies;

    /// <summary>A table whose turns go as scripted, the last one repeated.</summary>
    private sealed class Scripted(string run, params AgentTurn[] script) : IAgentTurnTaker
    {
        private int _taken;

        public string Run => run;

        public CancellationToken Stopping => CancellationToken.None;

        public string? LastFailure { get; init; }

        public int Taken => _taken;

        public Task<AgentTurn> TakeTurnAsync(CancellationToken cancellationToken = default)
        {
            var taken = ++_taken;
            return Task.FromResult(script[Math.Min(taken - 1, script.Length - 1)]);
        }
    }
}

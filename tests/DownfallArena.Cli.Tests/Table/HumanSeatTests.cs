using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The seat a person sits in: the driver asks it a question and blocks, and the answer arrives from the host's
/// thread. What these hold is that it answers the question it is waiting on and no other — a tap on a screen
/// that has moved on is late, not illegal, and the host can only say so if the seat can tell the difference.
/// </summary>
/// <remarks>
/// The seat reads neither the board nor the options; it asks the person, and the check has already held the
/// answer against the options that seat was served. Handing it none is how these say so.
/// </remarks>
public sealed class HumanSeatTests
{
    private static readonly CreatureId Creature = CreatureId.From(1);

    /// <summary>Which asking the seat is on, as a caller that has just read its screen would name.</summary>
    private static long Asking(HumanSeat seat) => seat.Waiting?.Asked ?? 0;

    [Fact]
    public void A_seat_nobody_is_asking_is_waiting_for_nothing_and_answers_nothing()
    {
        var seat = new HumanSeat(CancellationToken.None);

        seat.Waiting.ShouldBeNull();
        seat.Submit(PlayerDecision.ChooseSpeed(Creature, Speed.Quick), asked: 1).ShouldBeFalse();
    }

    [Fact]
    public async Task The_decision_a_seat_is_answered_with_is_the_one_the_match_receives()
    {
        var seat = new HumanSeat(TestContext.Current.CancellationToken);
        var asked = Task.Run(() => seat.DecideSpeed(null!, Creature), TestContext.Current.CancellationToken);

        await WaitingFor(seat, PlayerOptionsKind.Speed);
        seat.Submit(PlayerDecision.ChooseSpeed(Creature, Speed.Quick), Asking(seat)).ShouldBeTrue();

        (await asked).ShouldBe(Speed.Quick);
    }

    /// <summary>
    /// The seat stops waiting where it is answered rather than where the answer is picked up: the thread it
    /// unblocks needs a scheduler before it can say so, and a page polling in that window would be shown the
    /// question it just answered — then refused for answering it twice.
    /// </summary>
    [Fact]
    public async Task A_seat_stops_waiting_the_moment_it_is_answered()
    {
        var seat = new HumanSeat(TestContext.Current.CancellationToken);
        var asked = Task.Run(() => seat.DecideSpeed(null!, Creature), TestContext.Current.CancellationToken);

        await WaitingFor(seat, PlayerOptionsKind.Speed);
        var asking = Asking(seat);
        seat.Submit(PlayerDecision.ChooseSpeed(Creature, Speed.Standard), asking);

        seat.Waiting.ShouldBeNull();
        seat.Submit(PlayerDecision.ChooseSpeed(Creature, Speed.Quick), asking).ShouldBeFalse();
        await asked;
    }

    [Fact]
    public async Task A_decision_of_another_kind_than_the_question_is_not_an_answer_to_it()
    {
        var seat = new HumanSeat(TestContext.Current.CancellationToken);
        var asked = Task.Run(() => seat.DecideSpeed(null!, Creature), TestContext.Current.CancellationToken);

        await WaitingFor(seat, PlayerOptionsKind.Speed);

        seat.Submit(PlayerDecision.Pass, Asking(seat)).ShouldBeFalse();
        seat.Submit(PlayerDecision.DeclareIntent(Creature, SpellId.Parse("spell:pummel:v1")), Asking(seat)).ShouldBeFalse();
        seat.Submit(PlayerDecision.ChooseSpeed(Creature, Speed.Quick), Asking(seat)).ShouldBeTrue();
        await asked;
    }

    /// <summary>
    /// A decision for the previous asking is refused even when its shape fits the current one. Two Evolution
    /// picks in a round are the same shape, so without the asking a tap meant for the first would be accepted
    /// for the second — spending a pick nobody meant to spend. Checked here rather than at the caller, because
    /// anywhere outside this lock it is a check and then a gap.
    /// </summary>
    [Fact]
    public async Task A_decision_for_an_earlier_asking_of_the_same_shape_is_refused()
    {
        var seat = new HumanSeat(TestContext.Current.CancellationToken);
        var first = Task.Run(() => seat.DecideSpeed(null!, Creature), TestContext.Current.CancellationToken);
        await WaitingFor(seat, PlayerOptionsKind.Speed);
        var stale = Asking(seat);
        seat.Submit(PlayerDecision.ChooseSpeed(Creature, Speed.Quick), stale).ShouldBeTrue();
        await first;

        // The same shape asked again: the seat is on a new asking, and the old one's answer is not its.
        var second = Task.Run(() => seat.DecideSpeed(null!, Creature), TestContext.Current.CancellationToken);
        await WaitingFor(seat, PlayerOptionsKind.Speed);

        seat.Submit(PlayerDecision.ChooseSpeed(Creature, Speed.Standard), stale).ShouldBeFalse();
        seat.Submit(PlayerDecision.ChooseSpeed(Creature, Speed.Standard), Asking(seat)).ShouldBeTrue();
        (await second).ShouldBe(Speed.Standard);
    }

    /// <summary>A question is asked about one creature, and an answer for another one is not late — it is wrong.</summary>
    [Fact]
    public async Task A_decision_about_another_creature_than_the_one_asked_about_is_refused()
    {
        var seat = new HumanSeat(TestContext.Current.CancellationToken);
        var asked = Task.Run(() => seat.DecideSpeed(null!, Creature), TestContext.Current.CancellationToken);

        await WaitingFor(seat, PlayerOptionsKind.Speed);

        seat.Submit(PlayerDecision.ChooseSpeed(CreatureId.From(2), Speed.Quick), Asking(seat)).ShouldBeFalse();
        seat.Submit(PlayerDecision.ChooseSpeed(Creature, Speed.Quick), Asking(seat)).ShouldBeTrue();
        await asked;
    }

    /// <summary>The question the seat reports is the one the page has to answer, creature and all.</summary>
    [Fact]
    public async Task What_a_seat_is_waiting_for_names_the_creature_the_match_is_asking_about()
    {
        var seat = new HumanSeat(TestContext.Current.CancellationToken);
        var asked = Task.Run(() => seat.DecideSpeed(null!, Creature), TestContext.Current.CancellationToken);

        var waiting = await WaitingFor(seat, PlayerOptionsKind.Speed);

        waiting.Creature.ShouldBe(Creature);
        seat.Submit(PlayerDecision.ChooseSpeed(Creature, Speed.Quick), waiting.Asked);
        await asked;
    }

    /// <summary>A host that is stopping does not leave the driver blocked on a person who has gone home.</summary>
    [Fact]
    public async Task A_seat_whose_host_is_stopping_stops_waiting()
    {
        using var stopping = new CancellationTokenSource();
        var seat = new HumanSeat(stopping.Token);
        var asked = Task.Run(() => seat.DecideSpeed(null!, Creature), TestContext.Current.CancellationToken);

        await WaitingFor(seat, PlayerOptionsKind.Speed);
        await stopping.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(asked);
    }

    /// <summary>The seat is asked on another thread, so a test waits for the question rather than assuming it.</summary>
    private static async Task<HumanSeat.Question> WaitingFor(HumanSeat seat, PlayerOptionsKind kind)
    {
        while (seat.Waiting is not { } question || question.Kind != kind)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        return seat.Waiting!;
    }
}

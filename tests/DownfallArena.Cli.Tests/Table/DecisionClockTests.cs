using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The clock a decision's duration is measured on. It is the one number in a session that no other artifact
/// carries, and every way of getting it wrong makes "which decision was hard" unreadable rather than obviously
/// broken — so each way is a test.
/// </summary>
public sealed class DecisionClockTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 17, 20, 30, 0, TimeSpan.Zero);
    private static readonly HumanSeat.Question Evolution = new(PlayerOptionsKind.Evolution, Creature: null, Asked: 1);
    private static readonly HumanSeat.Question SpeedOfOne = new(PlayerOptionsKind.Speed, CreatureId.From(1), Asked: 2);

    /// <summary>The same shape asked again, which is a different question.</summary>
    private static readonly HumanSeat.Question EvolutionAgain = new(PlayerOptionsKind.Evolution, Creature: null, Asked: 3);

    [Fact]
    public void A_decision_is_timed_from_when_its_question_was_first_served()
    {
        var clock = new SteppingClock(Start);
        var stopwatch = new DecisionClock(clock);

        stopwatch.Served(PlayerSlot.Player1, Evolution);
        clock.Advance(TimeSpan.FromSeconds(4));

        stopwatch.ServedAt(PlayerSlot.Player1, Evolution).ShouldBe(Start);
    }

    /// <summary>A person reading the same question while the page polls every 700 ms keeps their own start.</summary>
    [Fact]
    public void Serving_the_same_question_again_does_not_restart_it()
    {
        var clock = new SteppingClock(Start);
        var stopwatch = new DecisionClock(clock);

        stopwatch.Served(PlayerSlot.Player1, Evolution);
        clock.Advance(TimeSpan.FromSeconds(2));
        stopwatch.Served(PlayerSlot.Player1, Evolution);
        clock.Advance(TimeSpan.FromSeconds(2));

        stopwatch.ServedAt(PlayerSlot.Player1, Evolution).ShouldBe(Start);
    }

    /// <summary>
    /// The race this exists for. A decision releases the driver before the host has written it down, so the
    /// next question can be asked and served while the first one's note is still being made. Taking whatever
    /// stamp is there would time the answered decision at nothing *and* rob the next one of its start, so one
    /// race would produce two wrong durations.
    /// </summary>
    [Fact]
    public void A_question_served_after_the_answer_is_not_taken_by_it()
    {
        var clock = new SteppingClock(Start);
        var stopwatch = new DecisionClock(clock);
        stopwatch.Served(PlayerSlot.Player1, Evolution);
        clock.Advance(TimeSpan.FromSeconds(5));

        // The driver has moved on and a poll has already stamped the next question, which replaced this
        // seat's one stamp. A caller reading the moment only now has already lost it -- which is why the
        // moment is read before the decision is handed over, and why this returns nothing rather than lying.
        stopwatch.Served(PlayerSlot.Player1, SpeedOfOne);

        stopwatch.ServedAt(PlayerSlot.Player1, Evolution).ShouldBeNull();

        // And forgetting the answered question must not take the next one's moment with it.
        stopwatch.Answered(PlayerSlot.Player1, Evolution);
        stopwatch.ServedAt(PlayerSlot.Player1, SpeedOfOne).ShouldBe(Start.AddSeconds(5));
    }

    /// <summary>A refusal leaves the seat on the same question, so the time spent being refused is part of it.</summary>
    [Fact]
    public void A_refusal_does_not_reset_the_question_it_was_refused_on()
    {
        var clock = new SteppingClock(Start);
        var stopwatch = new DecisionClock(clock);

        stopwatch.Served(PlayerSlot.Player1, Evolution);
        clock.Advance(TimeSpan.FromSeconds(6));
        stopwatch.Served(PlayerSlot.Player1, Evolution);

        stopwatch.ServedAt(PlayerSlot.Player1, Evolution).ShouldBe(Start);
    }

    /// <summary>
    /// The same question one round later is a new question. Its shape is identical — `Evolution` names no
    /// creature, so every round's is byte for byte the same — and it is only a new one because answering the
    /// first took its stamp away.
    /// </summary>
    [Fact]
    public void An_identical_question_a_round_later_is_timed_from_its_own_serving()
    {
        var clock = new SteppingClock(Start);
        var stopwatch = new DecisionClock(clock);
        stopwatch.Served(PlayerSlot.Player1, Evolution);
        stopwatch.Answered(PlayerSlot.Player1, Evolution);

        clock.Advance(TimeSpan.FromMinutes(3));
        stopwatch.Served(PlayerSlot.Player1, Evolution);
        clock.Advance(TimeSpan.FromSeconds(1));

        stopwatch.ServedAt(PlayerSlot.Player1, Evolution).ShouldBe(Start.AddMinutes(3));
    }

    /// <summary>
    /// Two askings of the same shape are two questions. A seat with two Evolution picks in a round is asked
    /// <c>(Evolution, null)</c> twice: a poll that catches the second while the first is still being written
    /// down must not have the second inherit the first's moment, and answering the first must not take the
    /// second's away.
    /// </summary>
    [Fact]
    public void The_same_shape_asked_again_is_a_new_question_with_its_own_moment()
    {
        var clock = new SteppingClock(Start);
        var stopwatch = new DecisionClock(clock);
        stopwatch.Served(PlayerSlot.Player1, Evolution);

        // The player answers the first while a poll catches the second.
        clock.Advance(TimeSpan.FromSeconds(9));
        stopwatch.Served(PlayerSlot.Player1, EvolutionAgain);

        stopwatch.ServedAt(PlayerSlot.Player1, EvolutionAgain).ShouldBe(Start.AddSeconds(9), "the second asking is timed from when it was served, not from the first");
        stopwatch.Answered(PlayerSlot.Player1, Evolution);
        stopwatch.ServedAt(PlayerSlot.Player1, EvolutionAgain).ShouldBe(Start.AddSeconds(9), "answering the first does not take the second's moment");
    }

    /// <summary>One seat's poll cannot start or take the other seat's clock; hotseat depends on it.</summary>
    [Fact]
    public void The_two_seats_are_timed_apart()
    {
        var clock = new SteppingClock(Start);
        var stopwatch = new DecisionClock(clock);

        stopwatch.Served(PlayerSlot.Player1, Evolution);
        clock.Advance(TimeSpan.FromSeconds(10));
        stopwatch.Served(PlayerSlot.Player2, Evolution);

        stopwatch.ServedAt(PlayerSlot.Player2, Evolution).ShouldBe(Start.AddSeconds(10));
        stopwatch.ServedAt(PlayerSlot.Player1, Evolution).ShouldBe(Start);
    }

    /// <summary>
    /// A seat waiting for nothing is forgotten, so whatever it is asked next starts fresh rather than
    /// continuing from a question it never answered.
    /// </summary>
    [Fact]
    public void A_seat_that_is_waiting_for_nothing_is_forgotten()
    {
        var clock = new SteppingClock(Start);
        var stopwatch = new DecisionClock(clock);
        stopwatch.Served(PlayerSlot.Player1, Evolution);

        clock.Advance(TimeSpan.FromSeconds(8));
        stopwatch.Served(PlayerSlot.Player1, question: null);

        stopwatch.ServedAt(PlayerSlot.Player1, Evolution).ShouldBeNull();
    }

    /// <summary>
    /// Nothing was ever served: a scripted seat, or a client posting without reading. Nothing rather than a
    /// moment, because "a person took no time" is a claim and this is the absence of one.
    /// </summary>
    [Fact]
    public void A_decision_whose_options_were_never_served_has_no_moment_to_be_timed_from()
    {
        new DecisionClock(new SteppingClock(Start)).ServedAt(PlayerSlot.Player1, Evolution).ShouldBeNull();
    }

    /// <summary>
    /// Answering a question the seat is not on leaves the stamp alone. It is the other half of the race above:
    /// the host reads the moment first and forgets it after, and a forget that fired on the wrong question
    /// would cost the next decision its start.
    /// </summary>
    [Fact]
    public void Forgetting_a_question_the_seat_is_no_longer_on_leaves_the_current_one_alone()
    {
        var clock = new SteppingClock(Start);
        var stopwatch = new DecisionClock(clock);
        stopwatch.Served(PlayerSlot.Player1, SpeedOfOne);

        stopwatch.Answered(PlayerSlot.Player1, Evolution);

        stopwatch.ServedAt(PlayerSlot.Player1, SpeedOfOne).ShouldBe(Start);
    }

    private sealed class SteppingClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}

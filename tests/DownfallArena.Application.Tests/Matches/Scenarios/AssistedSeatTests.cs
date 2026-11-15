using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using NSubstitute;

namespace DownfallArena.Application.Tests.Matches.Scenarios;

/// <summary>
/// The person's seat in a Tutorial (ADR 0187): the source's recorded answers for the questions the lesson does not
/// teach, while they fit what is asked, and the person's own for good once one does not.
/// </summary>
public sealed class AssistedSeatTests
{
    private static readonly CreatureId First = CreatureId.From(1);
    private static readonly CreatureId Third = CreatureId.From(3);

    [Theory]
    [InlineData(CombatDeclaration.AtActivation)]
    [InlineData(CombatDeclaration.HiddenIntent)]
    public async Task Assisted_questions_are_answered_while_they_fit_and_the_match_reaches_the_source_s_end(CombatDeclaration declaration)
    {
        var (replica, scenario, started, outcome) = await StartedAsync(declaration);
        var seat = AssistedSeat.Of(scenario, PlayerSlot.Player1, Teaching(PlayerOptionsKind.Action));
        var taught = new Queue<PlayerDecision>(Recorded(scenario, PlayerSlot.Player1).Where(decision => decision.Kind == PlayerOptionsKind.Action));
        var seats = new MatchSeats(null, ScriptedOpponent.Of(scenario, PlayerSlot.Player2, Substitute.For<IPlayerAgent>()));
        var matchId = started.Match.MatchId;
        var cancellation = TestContext.Current.CancellationToken;
        var step = (await replica.Stepper.AdvanceAsync(matchId, seats, HeldAnswers.None, cancellation)).Value;

        while (!step.IsOver)
        {
            var view = (await replica.Stepper.SeatAsync(matchId, PlayerSlot.Player1, step.Held, cancellation)).Value;
            var assisted = seat.Answer(view);
            step = (await replica.Stepper.SubmitAsync(matchId, seats, step.Held, new SeatDecision(PlayerSlot.Player1, assisted ?? taught.Dequeue()), cancellation)).Value;
            if (assisted is not null)
            {
                seat.Given();
            }
        }

        step.Outcome.ShouldBe(outcome);
        seat.HandedBack.ShouldBeFalse();
    }

    [Fact]
    public void A_question_the_lesson_teaches_is_left_to_the_person()
    {
        var seat = SeatOf(Teaching(PlayerOptionsKind.Intent), PlayerDecision.DeclareIntent(First, TestContent.Strike));

        var answer = seat.Answer(IntentView(First, TestContent.Strike));

        answer.ShouldBeNull();
        seat.HandedBack.ShouldBeFalse();
    }

    [Fact]
    public void A_seat_asked_nothing_gives_no_answer_and_is_not_handed_back()
    {
        var seat = SeatOf(Teaching(PlayerOptionsKind.Action), PlayerDecision.DeclareIntent(First, TestContent.Strike));

        var answer = seat.Answer(new SeatView(null!, new PlayerOptions { Kind = PlayerOptionsKind.Waiting }, null));

        answer.ShouldBeNull();
        seat.HandedBack.ShouldBeFalse();
    }

    [Fact]
    public void Asking_twice_before_the_answer_is_given_gives_the_same_answer()
    {
        var seat = SeatOf(Teaching(PlayerOptionsKind.Action), PlayerDecision.DeclareIntent(First, TestContent.Strike));
        var view = IntentView(First, TestContent.Strike, TestContent.Guard);

        var answers = new[] { seat.Answer(view), seat.Answer(view) };

        answers.ShouldBe([PlayerDecision.DeclareIntent(First, TestContent.Strike), PlayerDecision.DeclareIntent(First, TestContent.Strike)]);
        seat.HandedBack.ShouldBeFalse();
    }

    [Fact]
    public void A_given_answer_is_taken_off_the_script_and_the_next_one_answers_the_next_question()
    {
        var seat = SeatOf(Teaching(PlayerOptionsKind.Action), PlayerDecision.DeclareIntent(First, TestContent.Strike), PlayerDecision.DeclareIntent(CreatureId.From(2), TestContent.Guard));
        seat.Answer(IntentView(First, TestContent.Strike));
        seat.Given();

        var next = seat.Answer(IntentView(CreatureId.From(2), TestContent.Guard));

        next.ShouldBe(PlayerDecision.DeclareIntent(CreatureId.From(2), TestContent.Guard));
    }

    [Fact]
    public void A_recorded_answer_the_options_do_not_offer_hands_the_seat_back_for_good_and_says_so_once()
    {
        var seat = SeatOf(Teaching(PlayerOptionsKind.Action), PlayerDecision.DeclareIntent(First, TestContent.Slam), PlayerDecision.DeclareIntent(First, TestContent.Strike));
        var gaveBack = 0;
        seat.GaveBack += (_, _) => gaveBack++;
        var view = IntentView(First, TestContent.Strike, TestContent.Guard);

        var answers = new[] { seat.Answer(view), seat.Answer(view) };

        answers.ShouldBe([null, null]);
        seat.HandedBack.ShouldBeTrue();
        gaveBack.ShouldBe(1);
    }

    [Fact]
    public void A_recorded_answer_about_another_creature_hands_the_seat_back()
    {
        var seat = SeatOf(Teaching(PlayerOptionsKind.Action), PlayerDecision.DeclareIntent(CreatureId.From(2), TestContent.Strike));

        var answer = seat.Answer(IntentView(First, TestContent.Strike));

        answer.ShouldBeNull();
        seat.HandedBack.ShouldBeTrue();
    }

    [Fact]
    public void A_script_that_has_run_out_hands_the_seat_back()
    {
        var seat = SeatOf(Teaching(PlayerOptionsKind.Action));

        var answer = seat.Answer(IntentView(First, TestContent.Strike));

        answer.ShouldBeNull();
        seat.HandedBack.ShouldBeTrue();
    }

    [Fact]
    public void Recorded_answers_of_a_taught_kind_ahead_of_the_assisted_one_are_skipped()
    {
        var seat = SeatOf(
            Teaching(PlayerOptionsKind.Action),
            PlayerDecision.Act(First, TestContent.Slam, [Third]),
            PlayerDecision.Act(First, TestContent.Strike, [Third]),
            PlayerDecision.DeclareIntent(First, TestContent.Strike));

        var answer = seat.Answer(IntentView(First, TestContent.Strike));

        answer.ShouldBe(PlayerDecision.DeclareIntent(First, TestContent.Strike));
        seat.HandedBack.ShouldBeFalse();
    }

    [Fact]
    public void Once_handed_back_the_seat_stays_the_person_s_even_for_a_question_the_script_would_fit()
    {
        var seat = SeatOf(Teaching(PlayerOptionsKind.Action), PlayerDecision.DeclareIntent(First, TestContent.Slam), PlayerDecision.DeclareIntent(First, TestContent.Strike));
        seat.Answer(IntentView(First, TestContent.Strike));

        var answer = seat.Answer(IntentView(First, TestContent.Strike));

        answer.ShouldBeNull();
        seat.HandedBack.ShouldBeTrue();
    }

    private static AssistedSeat SeatOf(TutorialLesson lesson, params PlayerDecision[] recorded) => new(lesson, recorded);

    private static SeatView IntentView(CreatureId asked, params SpellId[] castable) =>
        new(null!, new PlayerOptions { Kind = PlayerOptionsKind.Intent, Intent = new IntentOptions([new IntentOption(asked, castable)]) }, new SeatQuestion(PlayerOptionsKind.Intent, asked));

    private static TutorialLesson Teaching(PlayerOptionsKind kind) =>
        TutorialLesson.Create(1, "A lesson", [kind], [new LessonStep(kind, 1, null, ["Look here."], [])]).Value;

    private static IEnumerable<PlayerDecision> Recorded(ScenarioCut scenario, PlayerSlot slot) =>
        scenario.After.OfType<DecisionEntry>().Where(entry => entry.Decision.Slot == slot).Select(entry => entry.Decision.Decision);

    /// <summary>A recorded match of two random agents, cut at its middle round and started under an id of its own.</summary>
    private static async Task<(Replica Replica, ScenarioCut Scenario, ScenarioStart Started, MatchOutcome? Outcome)> StartedAsync(CombatDeclaration declaration)
    {
        var replica = new Replica(new JournalStore());
        var rules = MatchStore.TwoOnTwo().WithCombatDeclaration(declaration);
        var source = new JournaledMatch("run-1", MatchId.New(), rules, 2, MatchStore.Roster(rules));
        var played = await replica.Journaled.AdvanceAsync(source, new MatchSeats(new RandomAgent(new TestRandom(3)), new RandomAgent(new TestRandom(4))), TestContext.Current.CancellationToken);
        IReadOnlyList<IJournalEntry> entries = [.. played.Value.Step.Decided.Select(decision => new DecisionEntry(decision))];
        var rounds = new ScenarioRounds(replica.Replay, replica.Stepper, replica.Store.Repository, []);
        var reading = await rounds.ReadAsync(source, entries, TestContext.Current.CancellationToken);
        var cut = await new ScenarioCutter(rounds, replica.Replay, replica.Stepper, replica.Store.Repository, [])
            .CutAsync(source, entries, reading.Rounds[reading.Rounds.Count / 2].Round, TestContext.Current.CancellationToken);
        var started = await new ScenarioStarter(replica.Replay, replica.Stepper).StartAsync(cut.Value, "scenario-1", MatchId.New(), TestContext.Current.CancellationToken);
        return (replica, cut.Value, started.Value, played.Value.Step.Outcome);
    }
}

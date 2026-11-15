using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A Tutorial's assisted answers at the host when the match does not take one (docs/tabletop/onboarding-plan.md,
/// O3a): the question must not be left to nobody, so every question goes back to the person.
/// </summary>
public sealed class TutorialPlayTests
{
    private static readonly CreatureId First = CreatureId.From(1);

    private static TutorialPlay Play() =>
        new(Lesson(), new AssistedSeat(Lesson(), [PlayerDecision.ChooseSpeed(First, Speed.Quick)]), PlayerSlot.Player1, of: 1);

    private static TutorialLesson Lesson() =>
        TutorialLesson.Create(1, "Your first combat", [PlayerOptionsKind.Action], [new LessonStep(PlayerOptionsKind.Action, 2, null, ["Pick a spell."], [])]).Value;

    /// <summary>A table whose person is asked the first creature's speed, an assisted question, and what it does with each answer.</summary>
    private static (TutorialPlay.Seat Table, List<(PlayerDecision Decision, long Asked)> Sent) AskedASpeed(Func<int, DecisionTaken> taken)
    {
        var board = new PlayerBoardState { MatchId = MatchId.New(), Slot = PlayerSlot.Player1, State = MatchState.InProgress, ContentHash = "test", RoundNumber = 2, Allies = [], Enemies = [] };
        var options = new PlayerOptions { Kind = PlayerOptionsKind.Speed, Speed = new SpeedOptions([First]) };
        var snapshot = new SeatSnapshot(new Asking(PlayerOptionsKind.Speed, First, 7), board, options, 0);
        var sent = new List<(PlayerDecision, long)>();
        return (new TutorialPlay.Seat(
            () => snapshot,
            () => false,
            (decision, asked) =>
            {
                sent.Add((decision, asked));
                return Task.FromResult(taken(sent.Count));
            }), sent);
    }

    private static async Task<bool> HandedBackAsync(TutorialPlay play)
    {
        for (var waited = 0; waited < 100 && play.Assisted.Count > 0; waited++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return play.Assisted.Count == 0;
    }

    [Fact]
    public async Task An_assisted_question_is_answered_with_the_recorded_answer_for_the_asking_it_was_read_at()
    {
        var (table, sent) = AskedASpeed(_ => new DecisionTaken(true, false));
        var play = Play();
        play.Follow(table);

        play.Look();

        // The script holds one answer and the table here keeps asking: the second reading finds none left.
        (await HandedBackAsync(play)).ShouldBeTrue();
        sent.ShouldBe([(PlayerDecision.ChooseSpeed(First, Speed.Quick), 7L)]);
    }

    [Fact]
    public async Task An_assisted_answer_that_comes_late_hands_every_question_back_to_the_person()
    {
        var (table, sent) = AskedASpeed(_ => DecisionTaken.Late);
        var play = Play();
        play.Follow(table);

        play.Look();

        (await HandedBackAsync(play)).ShouldBeTrue();
        sent.Count.ShouldBe(1);
    }

    [Fact]
    public async Task An_assisted_answer_that_cannot_be_written_hands_every_question_back_to_the_person()
    {
        var (table, _) = AskedASpeed(_ => throw new IOException("the journal is out"));
        var play = Play();
        play.Follow(table);

        play.Look();

        (await HandedBackAsync(play)).ShouldBeTrue();
    }

    [Fact]
    public void Before_anything_goes_wrong_every_kind_the_lesson_does_not_teach_is_assisted() =>
        Play().Assisted.ShouldBe(["Evolution", "Speed", "TieOrder", "Intent", "Target"]);
}

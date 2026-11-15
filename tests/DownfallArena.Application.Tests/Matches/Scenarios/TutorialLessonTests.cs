using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Matches.Scenarios;

/// <summary>A Tutorial's lesson (ADR 0187): what makes one, and each thing that keeps a block from being one.</summary>
public sealed class TutorialLessonTests
{
    private static readonly PlayerOptionsKind[] Action = [PlayerOptionsKind.Action];

    [Fact]
    public void A_lesson_assists_every_question_kind_it_does_not_teach()
    {
        var lesson = TutorialLesson.Create(2, " Who acts first ", [PlayerOptionsKind.Speed, PlayerOptionsKind.Action], [Step(PlayerOptionsKind.Speed)]).Value;

        (lesson.Order, lesson.Title).ShouldBe((2, "Who acts first"));
        lesson.Assists(PlayerOptionsKind.Evolution).ShouldBeTrue();
        lesson.Assists(PlayerOptionsKind.Speed).ShouldBeFalse();
        lesson.Assists(PlayerOptionsKind.Waiting).ShouldBeFalse();
    }

    [Fact]
    public void A_step_shows_at_its_kind_round_and_creature_and_at_any_creature_when_it_names_none()
    {
        var named = Step(PlayerOptionsKind.Action, creature: 2);
        var any = Step(PlayerOptionsKind.Action);

        named.ShowsAt(new SeatQuestion(PlayerOptionsKind.Action, CreatureId.From(2)), 1).ShouldBeTrue();
        named.ShowsAt(new SeatQuestion(PlayerOptionsKind.Action, CreatureId.From(3)), 1).ShouldBeFalse();
        named.ShowsAt(new SeatQuestion(PlayerOptionsKind.Action, CreatureId.From(2)), 2).ShouldBeFalse();
        named.ShowsAt(new SeatQuestion(PlayerOptionsKind.Speed, CreatureId.From(2)), 1).ShouldBeFalse();
        any.ShowsAt(new SeatQuestion(PlayerOptionsKind.Action, CreatureId.From(3)), 1).ShouldBeTrue();
    }

    [Fact]
    public void An_order_below_one_is_refused() =>
        TutorialLesson.Create(0, "A title", Action, [Step(PlayerOptionsKind.Action)]).Error.Code.ShouldBe("Tutorial.OrderNotPositive");

    [Fact]
    public void A_lesson_without_a_title_is_refused() =>
        TutorialLesson.Create(1, " ", Action, [Step(PlayerOptionsKind.Action)]).Error.Code.ShouldBe("Tutorial.NoTitle");

    [Fact]
    public void A_lesson_that_teaches_no_kind_is_refused() =>
        TutorialLesson.Create(1, "A title", [], [Step(PlayerOptionsKind.Action)]).Error.Code.ShouldBe("Tutorial.TeachesNothing");

    [Fact]
    public void A_lesson_that_names_a_kind_twice_is_refused() =>
        TutorialLesson.Create(1, "A title", [PlayerOptionsKind.Action, PlayerOptionsKind.Action], [Step(PlayerOptionsKind.Action)]).Error.Code.ShouldBe("Tutorial.TeachesNothing");

    [Fact]
    public void A_lesson_that_teaches_a_kind_nobody_is_asked_is_refused() =>
        TutorialLesson.Create(1, "A title", [PlayerOptionsKind.Waiting], [Step(PlayerOptionsKind.Waiting)]).Error.Code.ShouldBe("Tutorial.TeachesNothing");

    [Fact]
    public void A_step_in_no_round_is_refused() =>
        TutorialLesson.Create(1, "A title", Action, [Step(PlayerOptionsKind.Action) with { Round = 0 }]).Error.Code.ShouldBe("Tutorial.RoundNotPositive");

    [Fact]
    public void A_step_that_names_a_creature_for_a_question_asked_of_the_player_is_refused() =>
        TutorialLesson.Create(1, "A title", [PlayerOptionsKind.TieOrder], [Step(PlayerOptionsKind.TieOrder, creature: 1)]).Error.Code.ShouldBe("Tutorial.AskedOfThePlayer");

    [Fact]
    public void A_step_that_says_nothing_is_refused() =>
        TutorialLesson.Create(1, "A title", Action, [Step(PlayerOptionsKind.Action) with { Says = [] }]).Error.Code.ShouldBe("Tutorial.Lines");

    [Fact]
    public void A_step_with_an_empty_line_is_refused() =>
        TutorialLesson.Create(1, "A title", Action, [Step(PlayerOptionsKind.Action) with { Says = ["A line.", " "] }]).Error.Code.ShouldBe("Tutorial.Lines");

    [Fact]
    public void A_step_with_a_line_too_long_for_a_phone_is_refused() =>
        TutorialLesson.Create(1, "A title", Action, [Step(PlayerOptionsKind.Action) with { Says = [new string('a', TutorialLesson.LongestLine + 1)] }]).Error.Code.ShouldBe("Tutorial.Lines");

    [Fact]
    public void A_step_that_points_at_nothing_is_refused() =>
        TutorialLesson.Create(1, "A title", Action, [Step(PlayerOptionsKind.Action) with { PointsAt = new LessonPointer(null, null) }]).Error.Code.ShouldBe("Tutorial.PointsAtBoth");

    [Fact]
    public void A_step_about_any_creature_shares_its_question_with_a_step_about_one_and_is_refused() =>
        TutorialLesson.Create(1, "A title", Action, [Step(PlayerOptionsKind.Action, creature: 2), Step(PlayerOptionsKind.Action)]).Error.Code.ShouldBe("Tutorial.SameQuestion");

    [Fact]
    public void A_step_may_point_at_a_control() =>
        TutorialLesson.Create(1, "A title", Action, [Step(PlayerOptionsKind.Action) with { PointsAt = new LessonPointer(null, "spells") }]).IsSuccess.ShouldBeTrue();

    [Fact]
    public void A_step_that_points_at_a_control_the_page_does_not_have_is_refused() =>
        TutorialLesson.Create(1, "A title", Action, [Step(PlayerOptionsKind.Action) with { PointsAt = new LessonPointer(null, "spellbook") }]).Error.Code.ShouldBe("Tutorial.UnknownControl");

    private static LessonStep Step(PlayerOptionsKind kind, int? creature = null) =>
        new(kind, 1, creature is { } one ? CreatureId.From(one) : null, ["A line."], ["health"]);
}

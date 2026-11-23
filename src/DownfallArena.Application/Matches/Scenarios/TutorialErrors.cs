using DownfallArena.Application.Matches.Projections;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>Why a tutorial's lesson is not one, or does not hold on the board its scenario plays (ADR 0187).</summary>
public static class TutorialErrors
{
    public static readonly DomainError OrderNotPositive = new("Tutorial.OrderNotPositive", "A tutorial's order must be 1 or more.");

    public static DomainError SeriesTooShort(int of, int order) =>
        new("Tutorial.SeriesTooShort", $"The series length ({of}) is less than this tutorial's order ({order}).");

    public static DomainError OrderTooHigh(int order) =>
        new("Tutorial.OrderTooHigh", $"The tutorial's order ({order}) is more than the {TutorialLesson.LongestSeries} tutorials a series holds.");

    public static DomainError SeriesTooLong(int of) =>
        new("Tutorial.SeriesTooLong", $"The series length ({of}) is more than the {TutorialLesson.LongestSeries} tutorials a series holds.");

    public static readonly DomainError NoTitle = new("Tutorial.NoTitle", "A tutorial needs a title.");

    public static readonly DomainError TeachesNothing =
        new("Tutorial.TeachesNothing", "A tutorial must teach at least one kind of question and name each kind once: Evolution, Speed, TieOrder, Intent, Target or Action.");

    public static readonly DomainError NoStep = new("Tutorial.NoStep", "A tutorial needs at least one step.");

    public static DomainError KindNotTaught(int step, PlayerOptionsKind kind) =>
        new("Tutorial.KindNotTaught", $"Step {step} is set at a {kind} question. The lesson does not teach that kind: the person is never asked it.");

    public static DomainError RoundNotPositive(int step) => new("Tutorial.RoundNotPositive", $"Step {step} names a round below 1.");

    public static DomainError AskedOfThePlayer(int step, PlayerOptionsKind kind) =>
        new("Tutorial.AskedOfThePlayer", $"Step {step} names a creature for a {kind} question, which is asked of the player and not of one creature.");

    public static DomainError Lines(int step) =>
        new("Tutorial.Lines", $"Step {step} must say one or two lines, none empty and none over {TutorialLesson.LongestLine} characters.");

    public static DomainError UnknownConcept(int step, string concept) =>
        new("Tutorial.UnknownConcept", $"Step {step} opens the concept card '{concept}', which is not one of: {string.Join(", ", ConceptCards.Ids)}.");

    public static DomainError PointsAtBoth(int step) =>
        new("Tutorial.PointsAtBoth", $"Step {step} points at both a creature and a control, or at neither. It points at one or the other.");

    public static DomainError UnknownControl(int step, string control) =>
        new("Tutorial.UnknownControl", $"Step {step} points at the control '{control}', which is not one of: {string.Join(", ", LessonControls.Names)}.");

    public static DomainError SameQuestion(int step, int earlier) =>
        new("Tutorial.SameQuestion", $"Step {step} is set at the same question as step {earlier}. A question has at most one step.");

    public static DomainError CreatureNotOnBoard(int step, CreatureId creature) =>
        new("Tutorial.CreatureNotOnBoard", $"Step {step} is about creature {creature}, which is not on the scenario's board.");

    public static DomainError NotThePersonsCreature(int step, CreatureId creature) =>
        new("Tutorial.NotThePersonsCreature", $"Step {step} is set at a question about creature {creature}, which is not one of the person's: a seat is asked about its own creatures.");

    public static DomainError StepNeverReached(int step, LessonStep at) =>
        new("Tutorial.StepNeverReached", $"Step {step} is never reached. The recorded answers never ask the person's seat a {at.Kind} question in round {at.Round}{(at.Creature is { } creature ? $" about creature {creature}" : string.Empty)}.");

    public static DomainError StepsOutOfOrder(int step, int before) =>
        new("Tutorial.StepsOutOfOrder", $"Step {step} is reached before step {before}. Write the steps in the order the person reaches them.");
}

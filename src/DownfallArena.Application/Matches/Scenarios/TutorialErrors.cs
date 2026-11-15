using DownfallArena.Application.Matches.Projections;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>Why a tutorial's lesson is not one, or does not hold on the board its scenario plays (ADR 0187).</summary>
public static class TutorialErrors
{
    public static readonly DomainError OrderNotPositive = new("Tutorial.OrderNotPositive", "A tutorial's order is 1 or more.");

    public static readonly DomainError NoTitle = new("Tutorial.NoTitle", "A tutorial has a title.");

    public static readonly DomainError TeachesNothing =
        new("Tutorial.TeachesNothing", "A tutorial teaches at least one kind of question, each named once, among Evolution, Speed, TieOrder, Intent, Target and Action.");

    public static readonly DomainError NoStep = new("Tutorial.NoStep", "A tutorial has at least one step.");

    public static DomainError KindNotTaught(int step, PlayerOptionsKind kind) =>
        new("Tutorial.KindNotTaught", $"Step {step} shows at a {kind} question, which the lesson does not teach: it is answered for the person, who is never asked it.");

    public static DomainError RoundNotPositive(int step) => new("Tutorial.RoundNotPositive", $"Step {step} names no round: a round is 1 or more.");

    public static DomainError AskedOfThePlayer(int step, PlayerOptionsKind kind) =>
        new("Tutorial.AskedOfThePlayer", $"Step {step} names a creature for a {kind} question, which is asked of the player and not of one creature.");

    public static DomainError Lines(int step) =>
        new("Tutorial.Lines", $"Step {step} says one or two lines, none empty, none over {TutorialLesson.LongestLine} characters.");

    public static DomainError UnknownConcept(int step, string concept) =>
        new("Tutorial.UnknownConcept", $"Step {step} opens the concept card '{concept}', which is not one of: {string.Join(", ", ConceptCards.Ids)}.");

    public static DomainError PointsAtBoth(int step) =>
        new("Tutorial.PointsAtBoth", $"Step {step} points at a creature or at a control, one of the two.");

    public static DomainError SameQuestion(int step, int earlier) =>
        new("Tutorial.SameQuestion", $"Step {step} shows at the same question as step {earlier}: one step a question.");

    public static DomainError CreatureNotOnBoard(int step, CreatureId creature) =>
        new("Tutorial.CreatureNotOnBoard", $"Step {step} is about creature {creature}, which is not on the scenario's board.");

    public static DomainError StepNeverReached(int step, LessonStep at) =>
        new("Tutorial.StepNeverReached", $"Step {step} is never reached when the recorded answers are played: the person's seat is not asked a {at.Kind} question in round {at.Round}{(at.Creature is { } creature ? $" about creature {creature}" : string.Empty)}.");

    public static DomainError StepsOutOfOrder(int step, int before) =>
        new("Tutorial.StepsOutOfOrder", $"Step {step} is reached before step {before}: the steps are written in the order they show, and the last one written ends the tutorial.");
}

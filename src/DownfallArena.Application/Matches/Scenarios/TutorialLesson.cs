using DownfallArena.Application.Matches.Projections;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// The lesson block that makes a Repository scenario a Tutorial (ADR 0187): its order in the series, its title,
/// the question kinds it teaches and its Lesson steps, in the order they show. Every kind it does not teach is an
/// Assisted question, answered for the person from the source's recorded answers.
/// </summary>
public sealed record TutorialLesson
{
    /// <summary>The longest line a coach card holds on a phone.</summary>
    public const int LongestLine = 160;

    private static readonly PlayerOptionsKind[] Asked =
        [PlayerOptionsKind.Evolution, PlayerOptionsKind.Speed, PlayerOptionsKind.TieOrder, PlayerOptionsKind.Intent, PlayerOptionsKind.Target, PlayerOptionsKind.Action];

    private TutorialLesson(int order, int? of, string title, IReadOnlyList<PlayerOptionsKind> teaches, IReadOnlyList<LessonStep> steps)
    {
        Order = order;
        Of = of;
        Title = title;
        Teaches = teaches;
        Steps = steps;
    }

    /// <summary>Its place in the series, from 1.</summary>
    public int Order { get; }

    /// <summary>How many tutorials the series has, as this lesson says it (#1186); none in a file written before it.</summary>
    public int? Of { get; }

    public string Title { get; }

    /// <summary>The question kinds the person answers.</summary>
    public IReadOnlyList<PlayerOptionsKind> Teaches { get; }

    /// <summary>The steps, in the order they show; the last one shown ends the tutorial with its round.</summary>
    public IReadOnlyList<LessonStep> Steps { get; }

    /// <summary>Whether a question of this kind is answered for the person rather than asked.</summary>
    public bool Assists(PlayerOptionsKind kind) => Asked.Contains(kind) && !Teaches.Contains(kind);

    /// <summary>The lesson, or the first thing that keeps it from being one. Steps are numbered from 1.</summary>
    /// <param name="of">How many tutorials the series has; none when the file does not say. Never short of <paramref name="order" />.</param>
    public static Result<TutorialLesson> Create(int order, string? title, IReadOnlyList<PlayerOptionsKind> teaches, IReadOnlyList<LessonStep> steps, int? of = null)
    {
        ArgumentNullException.ThrowIfNull(teaches);
        ArgumentNullException.ThrowIfNull(steps);
        if (order < 1)
        {
            return Result.Failure<TutorialLesson>(TutorialErrors.OrderNotPositive);
        }

        if (of is { } length && length < order)
        {
            return Result.Failure<TutorialLesson>(TutorialErrors.SeriesTooShort(length, order));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<TutorialLesson>(TutorialErrors.NoTitle);
        }

        if (teaches.Count == 0 || teaches.Distinct().Count() != teaches.Count || teaches.Any(kind => !Asked.Contains(kind)))
        {
            return Result.Failure<TutorialLesson>(TutorialErrors.TeachesNothing);
        }

        if (steps.Count == 0)
        {
            return Result.Failure<TutorialLesson>(TutorialErrors.NoStep);
        }

        for (var index = 0; index < steps.Count; index++)
        {
            if (Refused(steps, index, teaches) is { } refusal)
            {
                return Result.Failure<TutorialLesson>(refusal);
            }
        }

        return Result.Success(new TutorialLesson(order, of, title.Trim(), [.. teaches], [.. steps]));
    }

    private static DomainError? Refused(IReadOnlyList<LessonStep> steps, int index, IReadOnlyList<PlayerOptionsKind> teaches)
    {
        var step = steps[index];
        var number = index + 1;
        if (!teaches.Contains(step.Kind))
        {
            return TutorialErrors.KindNotTaught(number, step.Kind);
        }

        if (step.Round < 1)
        {
            return TutorialErrors.RoundNotPositive(number);
        }

        if (step.Creature is not null && step.Kind is PlayerOptionsKind.Evolution or PlayerOptionsKind.TieOrder)
        {
            return TutorialErrors.AskedOfThePlayer(number, step.Kind);
        }

        if (step.Says.Count is < 1 or > 2 || step.Says.Any(line => string.IsNullOrWhiteSpace(line) || line.Length > LongestLine))
        {
            return TutorialErrors.Lines(number);
        }

        // A file's array can hold a null where a name was meant.
        if (step.Concepts.Any(concept => concept is null || !ConceptCards.Has(concept)))
        {
            return TutorialErrors.UnknownConcept(number, step.Concepts.First(concept => concept is null || !ConceptCards.Has(concept)) ?? "null");
        }

        if (step.PointsAt is { } pointer && (pointer.Creature is null) == string.IsNullOrWhiteSpace(pointer.Control))
        {
            return TutorialErrors.PointsAtBoth(number);
        }

        if (step.PointsAt?.Control is { } control && !string.IsNullOrWhiteSpace(control) && !LessonControls.Has(control))
        {
            return TutorialErrors.UnknownControl(number, control);
        }

        for (var earlier = 0; earlier < index; earlier++)
        {
            // A step about any creature shows at each creature's question of its kind and round.
            var other = steps[earlier];
            if ((other.Kind, other.Round) == (step.Kind, step.Round) && (other.Creature is null || step.Creature is null || other.Creature == step.Creature))
            {
                return TutorialErrors.SameQuestion(number, earlier + 1);
            }
        }

        return null;
    }
}

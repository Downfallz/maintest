using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// Holds a Tutorial's lesson against the board its scenario plays (ADR 0187): the scenario is started aside, its
/// source's recorded answers are played one at a time, and every Lesson step must show at a question the
/// person's seat is asked on the way, in the order the steps are written. Nothing is written and nobody is asked
/// anything.
/// </summary>
public sealed class LessonReach(ScenarioStarter starter, MatchReplay replay, MatchStepper stepper, IMatchRepository matches, IEnumerable<IMatchMemory> memories)
{
    /// <summary>
    /// Success when every step is reached in order. Refused with the scenario's own refusal when it does not
    /// start, <see cref="TutorialErrors.CreatureNotOnBoard" /> for a step about a creature the board does not
    /// hold, <see cref="TutorialErrors.NotThePersonsCreature" /> for a step at a question about an enemy, <see cref="TutorialErrors.StepsOutOfOrder" /> and <see cref="TutorialErrors.StepNeverReached" />.
    /// </summary>
    public async Task<Result> CheckAsync(ScenarioCut scenario, PlayerSlot seat, TutorialLesson lesson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(lesson);

        var aside = MatchId.New();
        try
        {
            var started = await starter.StartAsync(scenario, $"lesson-{aside}", aside, cancellationToken);
            return started.IsFailure ? Result.Failure(started.Error) : await PlayedAsync(started.Value.Match, scenario, seat, lesson, cancellationToken);
        }
        finally
        {
            await new ScenarioBoards.Aside(matches, memories).ForgetAsync(aside);
        }
    }

    private async Task<Result> PlayedAsync(JournaledMatch match, ScenarioCut scenario, PlayerSlot seat, TutorialLesson lesson, CancellationToken cancellationToken)
    {
        var steps = lesson.Steps;
        var held = HeldAnswers.None;
        var top = await stepper.SeatAsync(match.MatchId, seat, held, cancellationToken);
        if (top.IsFailure)
        {
            return Result.Failure(top.Error);
        }

        if (Stranger(steps, [.. top.Value.Board.Allies.Select(one => one.Id)], [.. top.Value.Board.Enemies.Select(one => one.Id)]) is { } stranger)
        {
            return Result.Failure(stranger);
        }

        var reached = 0;
        for (var line = 0; line < scenario.After.Count && reached < steps.Count; line++)
        {
            if (scenario.After[line] is DecisionEntry decided && decided.Decision.Slot == seat)
            {
                var view = await stepper.SeatAsync(match.MatchId, seat, held, cancellationToken);
                if (view.IsFailure)
                {
                    return Result.Failure(view.Error);
                }

                if (view.Value.Asked is { } asked && asked.Answers(decided.Decision.Decision) && view.Value.Board.RoundNumber is { } round)
                {
                    if (steps[reached].ShowsAt(asked, round))
                    {
                        reached++;
                    }
                    else if (Enumerable.Range(reached + 1, steps.Count - reached - 1).Where(index => steps[index].ShowsAt(asked, round)).Select(index => (int?)index).FirstOrDefault() is { } later)
                    {
                        return Result.Failure(TutorialErrors.StepsOutOfOrder(later + 1, reached + 1));
                    }
                }
            }

            var taken = await replay.ContinueAsync(match, held, [scenario.After[line]], scenario.Cut.CutAt + line, cancellationToken);
            if (taken.IsFailure)
            {
                return Result.Failure(taken.Error);
            }

            held = taken.Value;
        }

        return reached == steps.Count ? Result.Success() : Result.Failure(TutorialErrors.StepNeverReached(reached + 1, steps[reached]));
    }

    /// <summary>The first step about a creature, or pointing at one, that the board does not hold, or asked about an enemy.</summary>
    private static DomainError? Stranger(IReadOnlyList<LessonStep> steps, IReadOnlyList<CreatureId> allies, IReadOnlyList<CreatureId> enemies)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index].Creature is { } asked && !allies.Contains(asked))
            {
                return enemies.Contains(asked) ? TutorialErrors.NotThePersonsCreature(index + 1, asked) : TutorialErrors.CreatureNotOnBoard(index + 1, asked);
            }

            if (steps[index].PointsAt?.Creature is { } pointed && !allies.Contains(pointed) && !enemies.Contains(pointed))
            {
                return TutorialErrors.CreatureNotOnBoard(index + 1, pointed);
            }
        }

        return null;
    }
}

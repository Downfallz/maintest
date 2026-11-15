using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// Starts a match of its own from a match scenario (docs/tabletop/scenarios-plan.md, stage S1): created again
/// from the source's seed under a new id and handed the scenario's entries, so the random stream stands where it
/// stood at the cut, then held to the boards the source stood on there.
/// </summary>
public sealed class ScenarioStarter(MatchReplay replay, MatchStepper stepper)
{
    /// <summary>
    /// The scenario's match under <paramref name="into" />, kept as <paramref name="run" />. Refused with
    /// <see cref="ScenarioErrors.Diverged" /> when it does not rebuild, or does not stand on the boards of the
    /// scenario's checkpoint: the engine changed under the scenario, and nothing is started on a board nobody
    /// recorded.
    /// </summary>
    public async Task<Result<ScenarioStart>> StartAsync(ScenarioCut scenario, string run, MatchId into, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(run);

        var match = scenario.Source with { Run = run, MatchId = into };
        var standing = await ScenarioBoards.ReadAsync(replay, stepper, match, scenario.Before, scenario.Cut.Round, cancellationToken);
        if (standing.IsFailure)
        {
            return Result.Failure<ScenarioStart>(standing.Error.Code == JournalErrors.DivergedCode ? ScenarioErrors.Diverged : standing.Error);
        }

        // A seat that changed hands at the source's table is the source's seating, not the scenario's: the scenario
        // seats its own person and scripted opponent, and its journal begins with the match's own entries alone.
        return scenario.Checkpoint.StandsIn(standing.Value)
            ? Result.Success(new ScenarioStart(match, [.. scenario.Before.Where(entry => entry is not SeatChangeEntry)]))
            : Result.Failure<ScenarioStart>(ScenarioErrors.Diverged);
    }
}

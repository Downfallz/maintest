using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>Both seats' boards of a match rebuilt from some of its entries, as a scenario's cut keeps them.</summary>
internal static class ScenarioBoards
{
    public static async Task<Result<ScenarioCheckpoint>> AtAsync(MatchReplay replay, MatchStepper stepper, JournaledMatch match, IReadOnlyList<IJournalEntry> before, int round, CancellationToken cancellationToken)
    {
        var built = await replay.RebuildAsync(match, before, cancellationToken);
        if (built.IsFailure)
        {
            return Result.Failure<ScenarioCheckpoint>(built.Error);
        }

        var first = await stepper.SeatAsync(match.MatchId, PlayerSlot.Player1, built.Value, cancellationToken);
        if (first.IsFailure)
        {
            return Result.Failure<ScenarioCheckpoint>(first.Error);
        }

        var second = await stepper.SeatAsync(match.MatchId, PlayerSlot.Player2, built.Value, cancellationToken);
        return second.IsFailure
            ? Result.Failure<ScenarioCheckpoint>(second.Error)
            : Result.Success(new ScenarioCheckpoint(round, first.Value.Board, second.Value.Board));
    }
}

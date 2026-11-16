using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>Both seats' boards of a match rebuilt from some of its entries, as a scenario's cut keeps them.</summary>
internal static class ScenarioBoards
{
    /// <summary>
    /// The boards of <paramref name="match" /> rebuilt from <paramref name="before" /> under a match id of their own,
    /// forgotten once read: the source's own id may be a table a host holds in memory, which a rebuild under it
    /// would take back to the cut.
    /// </summary>
    public static async Task<Result<ScenarioCheckpoint>> AtAsync(MatchReplay replay, MatchStepper stepper, Aside aside, JournaledMatch match, IReadOnlyList<IJournalEntry> before, int round, CancellationToken cancellationToken)
    {
        var apart = match with { MatchId = MatchId.New() };
        try
        {
            return await ReadAsync(replay, stepper, apart, before, round, cancellationToken);
        }
        finally
        {
            await aside.ForgetAsync(apart.MatchId);
        }
    }

    /// <summary>
    /// What remembers a match set aside to be read: the repository that holds it and whatever kept its events,
    /// both told to let it go once it is read.
    /// </summary>
    public sealed record Aside(IMatchRepository Matches, IEnumerable<IMatchMemory> Memories)
    {
        public async Task ForgetAsync(MatchId matchId)
        {
            await Matches.ForgetAsync(matchId, CancellationToken.None);
            foreach (var memory in Memories)
            {
                memory.Forget(matchId);
            }
        }
    }

    /// <summary>The boards of <paramref name="match" /> rebuilt from <paramref name="before" />, under its own id.</summary>
    public static async Task<Result<ScenarioCheckpoint>> ReadAsync(MatchReplay replay, MatchStepper stepper, JournaledMatch match, IReadOnlyList<IJournalEntry> before, int round, CancellationToken cancellationToken)
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
            : Result.Success(ScenarioCheckpoint.Of(round, first.Value.Board, second.Value.Board));
    }
}

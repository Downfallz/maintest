using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// What a batch runner needs from a recorder: an agent wrapped before a match, and a call once the match ended.
/// </summary>
public interface IMatchRecorder
{
    /// <summary>
    /// Whether the batch may play its matches at the same time while this recorder watches them.
    /// <para>
    /// The recorder answers rather than the runner asking, because only the recorder knows whether what it
    /// writes is order-dependent. A recorder that appends every match to one artifact says no: the matches
    /// would interleave and a recorded run would stop replaying, which is the whole promise of a seed. A
    /// recorder that only tallies per match says yes.
    /// </para>
    /// </summary>
    bool AllowsParallelMatches => true;

    IPlayerAgent Wrap(MatchId matchId, IPlayerAgent agent);

    /// <summary>Closes the match: the board is the final one as player 1 sees it, carrying the outcome.</summary>
    Task MatchPlayedAsync(MatchId matchId, int seed, PlayerBoardState player1Board, CancellationToken cancellationToken = default);
}

using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// What a batch runner needs from a recorder: an agent wrapped before a match, and a call once the match ended.
/// </summary>
public interface IMatchRecorder
{
    IPlayerAgent Wrap(MatchId matchId, IPlayerAgent agent);

    /// <summary>Closes the match: the board is the final one as player 1 sees it, carrying the outcome.</summary>
    Task MatchPlayedAsync(MatchId matchId, int seed, PlayerBoardState player1Board, CancellationToken cancellationToken = default);
}

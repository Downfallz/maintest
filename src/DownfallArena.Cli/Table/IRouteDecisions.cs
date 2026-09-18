using DownfallArena.Application.Matches.Projections;

namespace DownfallArena.Cli.Table;

/// <summary>
/// An occupant that does not decide for itself but passes the board to somebody chosen from it, and can say
/// who that is without deciding anything.
/// </summary>
/// <remarks>
/// Deciding and naming the decider have to agree, and the only way to be sure they do is for the same rule to
/// answer both. A recorder that worked out who was playing from the outside would be a second copy of that
/// rule, and the copy that drifts is the one nobody reads.
/// </remarks>
internal interface IRouteDecisions
{
    Occupant DeciderOf(PlayerBoardState board);
}

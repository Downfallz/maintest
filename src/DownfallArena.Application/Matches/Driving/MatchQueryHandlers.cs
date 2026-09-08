using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Messaging;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// The read side of a match, bundled so a driver takes it as one dependency.
/// </summary>
public sealed record MatchQueryHandlers(
    IQueryHandler<GetBoardStateForPlayer, Result<PlayerBoardState>> GetBoardStateForPlayer,
    IQueryHandler<GetPlayerOptions, Result<PlayerOptions>> GetPlayerOptions);

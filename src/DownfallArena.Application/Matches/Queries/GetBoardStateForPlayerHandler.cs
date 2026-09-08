using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Messaging;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Queries;

public sealed class GetBoardStateForPlayerHandler(MatchWorkflow workflow) : IQueryHandler<GetBoardStateForPlayer, Result<PlayerBoardState>>
{
    public Task<Result<PlayerBoardState>> HandleAsync(GetBoardStateForPlayer query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return workflow.QueryAsync(query.MatchId, match => PlayerBoardStateProjection.Build(match, query.Slot), cancellationToken);
    }
}

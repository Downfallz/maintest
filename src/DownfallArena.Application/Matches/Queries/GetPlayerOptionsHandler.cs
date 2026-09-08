using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Queries;

public sealed class GetPlayerOptionsHandler(MatchWorkflow workflow, IGameResources resources) : IQueryHandler<GetPlayerOptions, Result<PlayerOptions>>
{
    public Task<Result<PlayerOptions>> HandleAsync(GetPlayerOptions query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return workflow.QueryAsync(query.MatchId, match => PlayerOptionsProjection.Build(match, query.Slot, resources), cancellationToken);
    }
}

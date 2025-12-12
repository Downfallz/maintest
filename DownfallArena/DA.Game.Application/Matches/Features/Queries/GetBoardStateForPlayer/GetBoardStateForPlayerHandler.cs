using AutoMapper;
using DA.Game.Application.Matches.Features.Queries.get_;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.Services.Queries;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.Queries.GetBoardStateForPlayer;

public sealed class GetBoardStateForPlayerHandler(
    IMatchRepository repo,
    IBoardStateQueryService boardStateQueryService,
    IMapper mapper)
    : IRequestHandler<GetBoardStateForPlayerQuery, Result<PlayerBoardStateView>>
{
    public async Task<Result<PlayerBoardStateView>> Handle(
        GetBoardStateForPlayerQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var match = await repo.GetAsync(query.MatchId, cancellationToken);
        if (match is null)
            return Result<PlayerBoardStateView>.Fail($"Match '{query.MatchId}' not found.");

        var boardStateRes = boardStateQueryService.GetBoardStateForPlayer(match, query.Slot);

        if (!boardStateRes.IsSuccess)
            return Result<PlayerBoardStateView>.Fail(boardStateRes.Error!);

        var view = mapper.Map<PlayerBoardStateView>(boardStateRes.Value!);

        return Result<PlayerBoardStateView>.Ok(view);
    }
}

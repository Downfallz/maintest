using AutoMapper;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.Services.Queries;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Utilities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.Features.Queries.GetPlayerOptions;

public sealed class GetPlayerOptionsHandler(
    IMatchRepository repo,
    IGameResources resources,
    IPlayerOptionsQueryService playerOptionsQueryService,
    IMapper mapper)
    : IRequestHandler<GetPlayerOptionsQuery, Result<PlayerOptionsView>>
{
    public async Task<Result<PlayerOptionsView>> Handle(
        GetPlayerOptionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var match = await repo.GetAsync(query.MatchId, cancellationToken);
        if (match is null)
            return Result<PlayerOptionsView>.Fail($"Match '{query.MatchId}' not found.");

        var optionsRes = playerOptionsQueryService.GetOptionsForPlayer(match, query.Slot, resources);
        if (!optionsRes.IsSuccess)
            return Result<PlayerOptionsView>.Fail(optionsRes.Error!);

        var view = mapper.Map<PlayerOptionsView>(optionsRes.Value!);
        return Result<PlayerOptionsView>.Ok(view);
    }
}

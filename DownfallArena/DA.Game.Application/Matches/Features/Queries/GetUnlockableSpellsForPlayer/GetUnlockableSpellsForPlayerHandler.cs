using AutoMapper;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.Services.Queries;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.Commands.SubmitEvolutionChoice;

public sealed class GetUnlockableSpellsForPlayerHandler(IMatchRepository repo,
    IGameResources resources,
    ITalentQueryService talentQueryService,
    IMapper mapper) : IRequestHandler<GetUnlockableSpellsForPlayerQuery, Result<PlayerUnlockableSpellsView>>
{
    public async Task<Result<PlayerUnlockableSpellsView>> Handle(GetUnlockableSpellsForPlayerQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var match = await repo.GetAsync(query.MatchId, cancellationToken);
        if (match is null)
            return Result<PlayerUnlockableSpellsView>.Fail($"Match '{query.MatchId}' not found.");

        var unlockableSpellsRes = talentQueryService.GetUnlockableSpellsForPlayer(match, query.slot, resources);

        if (!unlockableSpellsRes.IsSuccess)
            return Result<PlayerUnlockableSpellsView>.Fail(unlockableSpellsRes.Error!);

        var playerUnlockableSpellsView = mapper.Map<PlayerUnlockableSpellsView>(unlockableSpellsRes.Value!);


        return Result<PlayerUnlockableSpellsView>.Ok(playerUnlockableSpellsView);
    }
}

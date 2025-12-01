using AutoMapper;
using DA.Game.Application.Matches.Ports;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.SubmitCombatActionChoice;

public sealed class SubmitCombatIntentHandler(IMatchRepository repo,
    IMapper mapper) : IRequestHandler<SubmitCombatIntentCommand, Result<SubmitCombatIntentResult>>
{
    public async Task<Result<SubmitCombatIntentResult>> Handle(SubmitCombatIntentCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var match = await repo.GetAsync(cmd.MatchId, cancellationToken);
        if (match is null)
            return Result<SubmitCombatIntentResult>.Fail($"Match '{cmd.MatchId}' not found.");
        var domainChoice = mapper.Map<CombatActionIntent>(cmd.Intent);

        var res = match.SubmitCombatIntent(domainChoice);
        if (!res.IsSuccess)
            return Result<SubmitCombatIntentResult>.Fail(res.Error!);

        await repo.SaveAsync(match, cancellationToken);

        return Result<SubmitCombatIntentResult>.Ok(new SubmitCombatIntentResult());
    }
}

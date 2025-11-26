using AutoMapper;
using DA.Game.Application.Matches.Ports;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.SubmitCombatActionChoice;

public sealed class SubmitCombatActionChoiceHandler(IMatchRepository repo,
    IMapper mapper) : IRequestHandler<SubmitCombatActionChoiceCommand, Result<SubmitCombatActionResult>>
{
    public async Task<Result<SubmitCombatActionResult>> Handle(SubmitCombatActionChoiceCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var match = await repo.GetAsync(cmd.MatchId, cancellationToken);
        if (match is null)
            return Result<SubmitCombatActionResult>.Fail($"Match '{cmd.MatchId}' not found.");
        var domainChoice = mapper.Map<CombatActionChoice>(cmd.CombatActionChoice);

        var res = match.SubmitCombatAction(domainChoice);
        if (!res.IsSuccess)
            return Result<SubmitCombatActionResult>.Fail(res.Error!);

        await repo.SaveAsync(match, cancellationToken);

        return Result<SubmitCombatActionResult>.Ok(new SubmitCombatActionResult(cmd.CombatActionChoice, match.CurrentRound!.Phase));
    }
}

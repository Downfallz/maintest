using AutoMapper;
using DA.Game.Application.Matches.DTOs;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.SubmitCombatActionChoice;

public sealed class SubmitCombatActionChoiceHandler(IMatchRepository repo, 
    IApplicationEventCollector appEvents,
    IClock clock,
    IRandom rng,
    IMapper mapper) : IRequestHandler<SubmitCombatActionChoiceCommand, Result<SubmitCombatActionResult>>
{
    public async Task<Result<SubmitCombatActionResult>> Handle(SubmitCombatActionChoiceCommand cmd, CancellationToken ct = default)
    {
        var match = await repo.GetAsync(cmd.MatchId, ct);
        if (match is null)
            return Result<SubmitCombatActionResult>.Fail($"Match '{cmd.MatchId}' not found.");
        var domainChoice = mapper.Map<CombatActionChoice>(cmd.CombatActionChoice);

        var res = match.SubmitCombatAction(cmd.slot, domainChoice, clock);
        if (!res.IsSuccess)
            return Result<SubmitCombatActionResult>.Fail(res.Error!);

        await repo.SaveAsync(match, ct);

        return Result<SubmitCombatActionResult>.Ok(new SubmitCombatActionResult(cmd.CombatActionChoice, match.CurrentRound!.Phase));
    }
}

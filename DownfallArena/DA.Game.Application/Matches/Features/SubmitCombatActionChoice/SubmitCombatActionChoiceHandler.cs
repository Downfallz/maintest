using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Matches.Features.JoinMatch;

public sealed class SubmitCombatActionChoiceHandler(IMatchRepository repo, 
    IApplicationEventCollector appEvents,
    IClock clock,
    IRandom rng) : IRequestHandler<SubmitCombatActionChoiceCommand, Result<SubmitCombatActionResult>>
{
    public async Task<Result<SubmitCombatActionResult>> Handle(SubmitCombatActionChoiceCommand cmd, CancellationToken ct = default)
    {
        var match = await repo.GetAsync(cmd.MatchId, ct);
        if (match is null)
            return Result<SubmitCombatActionResult>.Fail($"Match '{cmd.MatchId}' not found.");
        var res = match.SubmitCombatAction(cmd.slot, cmd.CombatActionChoice, clock);
        if (!res.IsSuccess) return res;

        await repo.SaveAsync(match, ct);

        return res;
    }
}

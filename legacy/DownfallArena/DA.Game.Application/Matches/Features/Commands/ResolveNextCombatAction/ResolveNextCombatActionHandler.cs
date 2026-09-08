using AutoMapper;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.Commands.ResolveNextCombatAction;

public sealed class ResolveNextCombatActionHandler(IMatchRepository repo,
    IMapper mapper) : IRequestHandler<ResolveNextCombatActionCommand, Result<ResolveNextCombatActionResult>>
{
    public async Task<Result<ResolveNextCombatActionResult>> Handle(ResolveNextCombatActionCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var match = await repo.GetAsync(cmd.MatchId, cancellationToken);
        if (match is null)
            return Result<ResolveNextCombatActionResult>.Fail($"Match '{cmd.MatchId}' not found.");
        var stepOutcomeResult = match.ResolveNextCombatStep();
        if (!stepOutcomeResult.IsSuccess)
            return Result<ResolveNextCombatActionResult>.Fail(stepOutcomeResult.Error!);

        var view = mapper.Map<CombatStepOutcomeView>(stepOutcomeResult.Value);

        return Result<ResolveNextCombatActionResult>.Ok(new ResolveNextCombatActionResult(view));
    }
}

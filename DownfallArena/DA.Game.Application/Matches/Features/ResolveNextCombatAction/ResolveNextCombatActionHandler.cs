using AutoMapper;
using DA.Game.Application.Matches.Ports;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.SubmitSpeedChoice;

public sealed class ResolveNextCombatActionHandler(IMatchRepository repo) : IRequestHandler<ResolveNextCombatActionCommand, Result<ResolveNextCombatActionResult>>
{
    public async Task<Result<ResolveNextCombatActionResult>> Handle(ResolveNextCombatActionCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var match = await repo.GetAsync(cmd.MatchId, cancellationToken);
        if (match is null)
            return Result<ResolveNextCombatActionResult>.Fail($"Match '{cmd.MatchId}' not found.");
        var roundHasEnded = match.ResolveNextCombatStep();

        return Result<ResolveNextCombatActionResult>.Ok(new ResolveNextCombatActionResult(roundHasEnded.Value!));
    }
}

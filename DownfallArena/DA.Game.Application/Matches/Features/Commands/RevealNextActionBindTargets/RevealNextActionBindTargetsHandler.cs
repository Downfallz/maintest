using AutoMapper;
using DA.Game.Application.Matches.Ports;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.Commands.RevealNextActionBindTargets;

public sealed class RevealNextActionBindTargetsHandler(IMatchRepository repo) : IRequestHandler<RevealNextActionBindTargetsCommand, Result<RevealNextActionBindTargetsResult>>
{
    public async Task<Result<RevealNextActionBindTargetsResult>> Handle(RevealNextActionBindTargetsCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var match = await repo.GetAsync(cmd.MatchId, cancellationToken);
        if (match is null)
            return Result<RevealNextActionBindTargetsResult>.Fail($"Match '{cmd.MatchId}' not found.");

        var res = match.RevealNextActionAndBindTargets(cmd.TargetIds);
        if (!res.IsSuccess)
            return Result<RevealNextActionBindTargetsResult>.Fail(res.Error!);

        await repo.SaveAsync(match, cancellationToken);

        return Result<RevealNextActionBindTargetsResult>.Ok(new RevealNextActionBindTargetsResult());
    }
}

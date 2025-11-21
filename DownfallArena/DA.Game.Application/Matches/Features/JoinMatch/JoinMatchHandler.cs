using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.JoinMatch;

public sealed class JoinMatchHandler(IMatchRepository repo) : IRequestHandler<JoinMatchCommand, Result<JoinMatchResult>>
{
    public async Task<Result<JoinMatchResult>> Handle(JoinMatchCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var match = await repo.GetAsync(cmd.MatchId, cancellationToken);
        if (match is null)
            return Result<JoinMatchResult>.Fail($"Match '{cmd.MatchId}' not found.");
        var res = match.Join(cmd.PlayerRef);

        if (!res.IsSuccess)
            return Result<JoinMatchResult>.Fail(res.Error!);

        await repo.SaveAsync(match, cancellationToken);

        return Result<JoinMatchResult>.Ok(new JoinMatchResult(res.Value, match.State));
    }
}

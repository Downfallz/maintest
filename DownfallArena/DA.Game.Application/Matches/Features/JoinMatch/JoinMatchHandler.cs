using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Matches.Features.JoinMatch;

public sealed class JoinMatchHandler(IMatchRepository repo,
    IApplicationEventCollector appEvents,
    IClock clock,
    IRandom rng) : IRequestHandler<JoinMatchCommand, Result<JoinMatchResult>>
{
    public async Task<Result<JoinMatchResult>> Handle(JoinMatchCommand cmd, CancellationToken ct = default)
    {
        var match = await repo.GetAsync(cmd.MatchId, ct);
        if (match is null)
            return Result<JoinMatchResult>.Fail($"Match '{cmd.MatchId}' not found.");
        var res = match.Join(cmd.PlayerRef, clock, rng);

        if (!res.IsSuccess) return res;

        await repo.SaveAsync(match, ct);

        return res;
    }
}

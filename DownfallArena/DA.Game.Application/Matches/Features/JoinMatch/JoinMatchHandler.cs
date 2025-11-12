using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Players.Features.Create.Notifications;
using DA.Game.Application.Shared.Abstractions;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Application.Shared.Primitives;
using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Matches.Features.JoinMatch;

public sealed class JoinMatchHandler(IMatchRepository repo, 
    IApplicationEventCollector appEvents,
    IClock clock,
    IRandom rng) : IRequestHandler<JoinMatchCommand, Result<MatchState>>
{
    public async Task<Result<MatchState>> Handle(JoinMatchCommand cmd, CancellationToken ct = default)
    {
        var match = await repo.GetAsync(cmd.MatchId, ct) ?? new Match(cmd.MatchId);
        var res = match.Join(cmd.PlayerRef, clock, rng);
        if (!res.IsSuccess) return res;

        await repo.SaveAsync(match, ct);

        return Result<MatchState>.Ok(match.State);
    }
}

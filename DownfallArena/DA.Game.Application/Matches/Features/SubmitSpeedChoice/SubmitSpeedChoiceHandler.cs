using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Matches.Features.JoinMatch;

public sealed class SubmitSpeedChoiceHandler(IMatchRepository repo, 
    IApplicationEventCollector appEvents,
    IClock clock,
    IRandom rng) : IRequestHandler<SubmitSpeedChoiceCommand, Result<SubmitSpeedResult>>
{
    public async Task<Result<SubmitSpeedResult>> Handle(SubmitSpeedChoiceCommand cmd, CancellationToken ct = default)
    {
        var match = await repo.GetAsync(cmd.MatchId, ct);
        if (match is null)
            return Result<SubmitSpeedResult>.Fail($"Match '{cmd.MatchId}' not found.");
        var res = match.SubmitSpeedChoice(cmd.slot, cmd.SpeedChoice, clock);
        if (!res.IsSuccess) return res;

        await repo.SaveAsync(match, ct);

        return res;
    }
}

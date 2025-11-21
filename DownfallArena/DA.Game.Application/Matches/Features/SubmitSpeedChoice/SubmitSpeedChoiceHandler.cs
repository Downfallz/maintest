using AutoMapper;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.SubmitSpeedChoice;

public sealed class SubmitSpeedChoiceHandler(IMatchRepository repo, 
    IMapper mapper) : IRequestHandler<SubmitSpeedChoiceCommand, Result<SubmitSpeedResult>>
{
    public async Task<Result<SubmitSpeedResult>> Handle(SubmitSpeedChoiceCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var match = await repo.GetAsync(cmd.MatchId, cancellationToken);
        if (match is null)
            return Result<SubmitSpeedResult>.Fail($"Match '{cmd.MatchId}' not found.");

        var domainChoice = mapper.Map<SpeedChoice>(cmd.SpeedChoice);
        var res = match.SubmitSpeedChoice(cmd.slot, domainChoice);
        if (!res.IsSuccess)
            return Result<SubmitSpeedResult>.Fail(res.Error!);

        await repo.SaveAsync(match, cancellationToken);
        var choices = cmd.slot == PlayerSlot.Player1 ? match.CurrentRound!.Player1SpeedChoices : match.CurrentRound!.Player2SpeedChoices;
        return Result<SubmitSpeedResult>.Ok(new SubmitSpeedResult(choices.ToHashSet(), match.CurrentRound!.Phase));
    }
}

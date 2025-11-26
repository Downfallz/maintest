using DA.Game.Application.Matches.Features.PlayTurn;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.Events.Speed;
using DA.Game.Shared.Contracts.Matches.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace DA.Game.Application.Matches.DomainEvents;

public sealed class SpeedPhaseCompletedHandler(
    IMatchRepository repo) : INotificationHandler<SpeedPhaseCompleted>
{

    public async Task Handle(SpeedPhaseCompleted evt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var match = await repo.GetAsync(evt.MatchId, cancellationToken);
        if (match is null) return;

        match.InitializeCurrentRoundCombatPhase();

        await repo.SaveAsync(match, cancellationToken);
    }
}


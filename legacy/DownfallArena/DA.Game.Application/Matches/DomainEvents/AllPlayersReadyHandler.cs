using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.Events.Match;
using DA.Game.Shared.Contracts.Matches.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace DA.Game.Application.Matches.DomainEvents;

public sealed class AllPlayersReadyHandler(
    IMatchRepository repo) : INotificationHandler<AllPlayersReady>
{

    public async Task Handle(AllPlayersReady evt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var match = await repo.GetAsync(evt.MatchId, cancellationToken);
        if (match is null) return;

        match.StartMatch();

        await repo.SaveAsync(match, cancellationToken);
    }
}


using DA.Game.Application.Matches.Features.PlayTurn;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.Events;
using DA.Game.Shared.Contracts.Matches.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace DA.Game.Application.Matches.DomainEvents;

public sealed class TurnAdvancedHandler(
    IMatchRepository repo,
    ITurnDeciderRegistry turnDeciderRegistry,
    IMediator commandBus,
    IOptions<GameSettings> options) : INotificationHandler<TurnAdvanced>
{
    private readonly bool _simulation = options.Value.SimulationMode;

    public async Task Handle(TurnAdvanced evt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (!_simulation)
        {
            var match = await repo.GetAsync(evt.MatchId, cancellationToken);
            if (match is null || match.State != MatchState.Started) return;
            // fix avec le combat timeline...

            //var currentRef = match.PlayerRef2 == PlayerSlot.Player1 ? match.PlayerRef1 : match.PlayerRef2; //todo : fix

            var currentRef = match.PlayerRef1; // À corriger avec le timeline
            if (currentRef is null) return;
            // Résolution du decider
            var decider = turnDeciderRegistry.Resolve(currentRef.Kind); // ou Resolve(currentRef.Id)

            var view = new GameView(match.Id, PlayerSlot.Player1, match.RoundNumber,
                                    match.PlayerRef1?.Id, match.PlayerRef2?.Id);

            var action = await decider.DecideAsync(currentRef.Id, view, cancellationToken);

            // Si c’est humain => pas d’action auto (on attend l’UI).
            if (action is null) return;

            // Sinon, bot : passe par le même pipeline que l’humain
            await commandBus.Send(new PlayTurnCommand(match.Id, currentRef.Id, action), cancellationToken);
        }
    }
}


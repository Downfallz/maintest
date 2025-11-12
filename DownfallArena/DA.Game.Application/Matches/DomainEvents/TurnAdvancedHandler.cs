using DA.Game.Application.Matches.Features.PlayTurn;
using DA.Game.Application.Matches.Ports;
using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Match.Events;
using DA.Game.Domain2.Match.ReadModels;
using DA.Game.Shared;
using MediatR;
using Microsoft.Extensions.Options;

namespace DA.Game.Application.Matches.Events;

public sealed class TurnAdvancedHandler(
    IMatchRepository repo,
    ITurnDeciderRegistry turnDeciderRegistry,
    IClock clock,
    IMediator commandBus,
    IOptions<GameSettings> options) : INotificationHandler<TurnAdvanced>
{
    private readonly bool _simulation = options.Value.SimulationMode;

    public async Task Handle(TurnAdvanced evt, CancellationToken ct = default)
    {
        if (!_simulation)
        {
            var match = await repo.GetAsync(evt.MatchId, ct);
            if (match is null || match.CurrentPlayerSlot is null || match.State != MatchState.Started) return;

            var currentRef = match.CurrentPlayerSlot == PlayerSlot.Player1 ? match.PlayerRef1 : match.PlayerRef2;
            if (currentRef is null) return;
            // Résolution du decider
            var decider = turnDeciderRegistry.Resolve(currentRef.Kind); // ou Resolve(currentRef.Id)

            var view = new GameView(match.Id, match.CurrentPlayerSlot, match.RoundNumber,
                                    match.PlayerRef1?.Id, match.PlayerRef2?.Id);

            var action = await decider.DecideAsync(currentRef.Id, view, ct);

            // Si c’est humain => pas d’action auto (on attend l’UI).
            if (action is null) return;

            // Sinon, bot : passe par le même pipeline que l’humain
            await commandBus.Send(new PlayTurnCommand(match.Id, currentRef.Id, action));
        }
    }
}


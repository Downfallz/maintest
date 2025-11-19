using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Players.Ids;

namespace DA.Game.Application.Matches.Ports;
public interface ITurnDecider
{
    /// <summary>
    /// Donne une action si l’acteur est autonome (bot).
    /// Retourne null si humain (l’UI déclenchera PlayTurnCommand).
    /// </summary>
    Task<PlayerAction?> DecideAsync(
        PlayerId playerId,
        GameView view,
        CancellationToken ct = default);
}
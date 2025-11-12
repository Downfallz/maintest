using DA.Game.Domain2.Match.ReadModels;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Players.Ids;

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
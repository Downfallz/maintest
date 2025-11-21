using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Players.Ids;

namespace DA.Game.Application;

public sealed class HumanTurnDecider : ITurnDecider
{
    public Task<PlayerAction?> DecideAsync(PlayerId playerId, GameView view, CancellationToken ct = default)
        => Task.FromResult<PlayerAction?>(null); // humain : pas d’auto
}
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Players.Ids;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application;
public sealed class RandomBotDecider : ITurnDecider
{
    private readonly IRandom _rng;
    public RandomBotDecider(IRandom rng) => _rng = rng;

    public Task<PlayerAction?> DecideAsync(PlayerId playerId, GameView view, CancellationToken ct = default)
        => Task.FromResult<PlayerAction?>(new PlayerAction("noop", $"bot-roll:{_rng.Next(0, 100)}"));
}

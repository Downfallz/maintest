using DA.Game.Application.Matches.Ports;
using DA.Game.Domain2.Match.ReadModels;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Players.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Infrastructure.Matches;
public sealed class HumanTurnDecider : ITurnDecider
{
    public Task<PlayerAction?> DecideAsync(PlayerId playerId, GameView view, CancellationToken ct = default)
        => Task.FromResult<PlayerAction?>(null); // humain : pas d’auto
}
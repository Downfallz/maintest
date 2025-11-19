using DA.Game.Application.Learning.Abstractions;
using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Players.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Learning.ML;
public sealed class MLBotDecider : ITurnDecider
{
    private readonly IPolicy _policy;
    public MLBotDecider(IPolicy policy) => _policy = policy;

    public async Task<PlayerAction?> DecideAsync(PlayerId playerId, GameView view, CancellationToken ct = default)
        => await _policy.DecideAsync(view, ct);
}


using DA.Game.Application.Matches.Ports;
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Contracts.Players.Ids;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application;
public sealed class TurnDeciderRegistry : ITurnDeciderRegistry
{
    private readonly ConcurrentDictionary<PlayerId, ITurnDecider> _byPlayer = new();
    private readonly ConcurrentDictionary<ActorKind, ITurnDecider> _byKind = new();
    private readonly ITurnDecider _default;

    public TurnDeciderRegistry(ITurnDecider defaultDecider)
    {
        _default = defaultDecider;
    }

    // API d’enregistrement (optionnelles si tu configures via DI)
    public TurnDeciderRegistry Register(PlayerId id, ITurnDecider decider)
    { _byPlayer[id] = decider; return this; }

    public TurnDeciderRegistry Register(ActorKind kind, ITurnDecider decider)
    { _byKind[kind] = decider; return this; }

    public ITurnDecider Resolve(PlayerId playerId)
        => _byPlayer.TryGetValue(playerId, out var d) ? d : _default;

    public ITurnDecider Resolve(ActorKind kind)
        => _byKind.TryGetValue(kind, out var d) ? d : _default;
}
using DA.Game.Shared.Contracts.Players.Enums;
using DA.Game.Shared.Contracts.Players.Ids;

namespace DA.Game.Application.Matches.Ports;

public interface ITurnDeciderRegistry
{
    ITurnDecider Resolve(PlayerId playerId);
    ITurnDecider Resolve(ActorKind kind);
}
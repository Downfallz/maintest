using DA.Game.Domain2.Players.Enums;
using DA.Game.Domain2.Players.Ids;

namespace DA.Game.Application.Matches.Ports;
public interface ITurnDeciderRegistry
{
    ITurnDecider Resolve(PlayerId playerId);
    ITurnDecider Resolve(ActorKind kind);
}
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Players.Ids;
using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared;

namespace DA.Game.Domain2.Match.Events;

public sealed record PlayerJoined(MatchId MatchId, PlayerId PlayerId, DateTime dt) : EventBase(dt), IDomainEvent;


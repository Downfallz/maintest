using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared;

namespace DA.Game.Domain2.Match.Events;

public sealed record TurnAdvanced(MatchId MatchId, int TurnNumber, DateTime dt) : EventBase(dt), IDomainEvent;

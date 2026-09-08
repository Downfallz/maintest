using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Events.Match;

public sealed record MatchStarted(MatchId MatchId, DateTime dt) : EventBase(dt), IDomainEvent;

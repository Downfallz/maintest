using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Events;

public sealed record SpeedResolutionCompleted(MatchId MatchId, RoundId RoundId, int RoundNumber, DateTime dt) : EventBase(dt), IDomainEvent;

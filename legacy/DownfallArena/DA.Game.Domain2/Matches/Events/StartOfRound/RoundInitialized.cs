using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Events.StartOfRound;

public sealed record RoundInitialized(MatchId MatchId, RoundId RoundId, int TurnNumber, DateTime dt) : EventBase(dt), IDomainEvent;

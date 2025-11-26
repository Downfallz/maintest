using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Events.Combat;

public sealed record RoundCombatCompleted(RoundId RoundId, int RoundNumber, DateTime UtcTime) : EventBase(UtcTime), IDomainEvent;
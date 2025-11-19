using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared;

namespace DA.Game.Domain2.Match.Events;

public sealed record CombatActionResolved(RoundId RoundId, CombatActionResult CombatActionSummary, DateTime dt) : EventBase(dt), IDomainEvent;

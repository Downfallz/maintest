using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Events.Combat;

public sealed record CombatActionResolved(RoundId RoundId, CombatActionResult CombatActionSummary, DateTime dt) : EventBase(dt), IDomainEvent;

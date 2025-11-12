using DA.Game.Domain2.Catalog.Ids;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Ids;
using DA.Game.Domain2.Shared.Messaging;

namespace DA.Game.Domain2.Matches.Events;

public sealed record ActionResolved(RoundId RoundId, CharacterId ActorId, SpellId SpellId, CombatActionResult Result, DateTime UtcTime) : IDomainEvent
{
    public Guid EventId => throw new NotImplementedException();

    public DateTime OccurredAt => throw new NotImplementedException();
}

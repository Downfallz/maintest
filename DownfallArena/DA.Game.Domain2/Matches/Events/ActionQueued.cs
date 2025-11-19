using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared.Contracts.Catalog.Ids;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Events;
public sealed record ActionQueued(RoundId RoundId, CharacterId ActorId, SpellId SpellId, DateTime UtcTime) : IDomainEvent
{
    public Guid EventId => throw new NotImplementedException();

    public DateTime OccurredAt => throw new NotImplementedException();
}

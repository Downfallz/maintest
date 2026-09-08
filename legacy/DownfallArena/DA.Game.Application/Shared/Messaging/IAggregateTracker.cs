using DA.Game.Domain2.Shared.Messaging;

namespace DA.Game.Application.Shared.Messaging;

public interface IAggregateTracker
{
    void Track(IHasDomainEvents aggregate);
    IReadOnlyCollection<IHasDomainEvents> DequeueAll();
}


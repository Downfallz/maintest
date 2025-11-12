namespace DA.Game.Domain2.Shared.Messaging;
public interface IAggregateTracker
{
    void Track(IHasDomainEvents aggregate);
    IReadOnlyCollection<IHasDomainEvents> DequeueAll();
}


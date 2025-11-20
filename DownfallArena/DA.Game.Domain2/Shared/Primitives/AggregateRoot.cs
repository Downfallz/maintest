using DA.Game.Domain2.Shared.Messaging;

namespace DA.Game.Domain2.Shared.Primitives;

public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id), IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected void AddEvent(IDomainEvent evt) => _domainEvents.Add(evt);

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public IReadOnlyCollection<IDomainEvent> DequeueEvents()
    {
        var snapshot = _domainEvents.ToArray();
        _domainEvents.Clear();
        return snapshot;
    }
}

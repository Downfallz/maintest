namespace DA.Game.Domain2.Shared.Messaging;
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    IReadOnlyCollection<IDomainEvent> DequeueEvents();
}

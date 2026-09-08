using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Messaging;

/// <summary>
/// Reacts to one kind of domain event after the aggregate that raised it was saved. Implement through
/// <see cref="DomainEventListener{TEvent}"/>; the dispatcher matches listeners on <see cref="EventType"/>.
/// (Named a listener rather than a handler: the <c>EventHandler</c> suffix is reserved for delegates.)
/// </summary>
public interface IDomainEventListener
{
    Type EventType { get; }

    Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

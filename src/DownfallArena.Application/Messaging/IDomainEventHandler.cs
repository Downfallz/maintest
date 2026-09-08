using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Messaging;

/// <summary>
/// Reacts to one kind of domain event after the aggregate that raised it was saved. Implement through
/// <see cref="DomainEventHandler{TEvent}"/>; the dispatcher matches handlers on <see cref="EventType"/>.
/// </summary>
public interface IDomainEventHandler
{
    Type EventType { get; }

    Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

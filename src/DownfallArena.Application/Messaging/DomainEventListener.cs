using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Messaging;

/// <summary>
/// Base for typed domain event listeners: events of another type are ignored.
/// </summary>
public abstract class DomainEventListener<TEvent> : IDomainEventListener
    where TEvent : IDomainEvent
{
    public Type EventType => typeof(TEvent);

    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return domainEvent is TEvent typed ? HandleAsync(typed, cancellationToken) : Task.CompletedTask;
    }

    protected abstract Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

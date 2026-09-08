using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Messaging;

/// <summary>
/// Dispatches to every registered <see cref="IDomainEventHandler"/> whose event type matches. No reflection,
/// no pipeline: handlers are plain registrations.
/// </summary>
public sealed class DomainEventDispatcher(IEnumerable<IDomainEventHandler> handlers) : IDomainEventDispatcher
{
    private readonly IReadOnlyList<IDomainEventHandler> _handlers = [.. handlers];

    public async Task DispatchAsync<TId>(AggregateRoot<TId> aggregate, CancellationToken cancellationToken = default)
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        var events = aggregate.DomainEvents.ToList();
        aggregate.ClearDomainEvents();

        foreach (var domainEvent in events)
        {
            foreach (var handler in _handlers.Where(handler => handler.EventType.IsInstanceOfType(domainEvent)))
            {
                await handler.HandleAsync(domainEvent, cancellationToken);
            }
        }
    }
}

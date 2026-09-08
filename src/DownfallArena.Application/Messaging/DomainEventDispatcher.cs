using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Messaging;

/// <summary>
/// Dispatches to every registered <see cref="IDomainEventListener"/> whose event type matches. No reflection,
/// no pipeline: listeners are plain registrations.
/// </summary>
public sealed class DomainEventDispatcher(IEnumerable<IDomainEventListener> listeners) : IDomainEventDispatcher
{
    private readonly IReadOnlyList<IDomainEventListener> _listeners = [.. listeners];

    public async Task DispatchAsync<TId>(AggregateRoot<TId> aggregate, CancellationToken cancellationToken = default)
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        var events = aggregate.DomainEvents.ToList();
        aggregate.ClearDomainEvents();

        foreach (var domainEvent in events)
        {
            foreach (var listener in _listeners.Where(listener => listener.EventType.IsInstanceOfType(domainEvent)))
            {
                await listener.HandleAsync(domainEvent, cancellationToken);
            }
        }
    }
}

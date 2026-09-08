using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Messaging;

/// <summary>
/// Hands the events an aggregate raised to their listeners, in order, and clears them once all were handled
/// (ADR 0008). A listener that throws leaves the events on the aggregate.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync<TId>(AggregateRoot<TId> aggregate, CancellationToken cancellationToken = default)
        where TId : notnull;
}

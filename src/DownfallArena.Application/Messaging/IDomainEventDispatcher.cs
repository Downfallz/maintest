using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Messaging;

/// <summary>
/// Hands the events an aggregate raised to their handlers, in order, then clears them (ADR 0008).
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync<TId>(AggregateRoot<TId> aggregate, CancellationToken cancellationToken = default)
        where TId : notnull;
}

using System.Collections.Concurrent;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Infrastructure.Tests.Invariants;

/// <summary>
/// Every event of every match, in the order each match raised them. Concurrent because a batch plays its
/// matches at once; one match's events still arrive one after another, since a match is driven in sequence.
/// </summary>
internal sealed class MatchEventLog : IDomainEventListener
{
    private readonly ConcurrentDictionary<MatchId, ConcurrentQueue<IMatchEvent>> _events = new();

    public Type EventType => typeof(IMatchEvent);

    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (domainEvent is IMatchEvent matchEvent)
        {
            _events.GetOrAdd(matchEvent.MatchId, _ => new ConcurrentQueue<IMatchEvent>()).Enqueue(matchEvent);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<IMatchEvent> Of(MatchId matchId) => _events.TryGetValue(matchId, out var events) ? [.. events] : [];
}

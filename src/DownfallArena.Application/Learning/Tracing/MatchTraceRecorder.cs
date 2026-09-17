using System.Collections.Concurrent;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Learning.Tracing;

/// <summary>
/// A domain event listener that keeps, per match, every event with both players' boards after it. Events are
/// dispatched once the command that raised them is saved, so the boards are the ones after that command.
/// <see cref="Complete"/> hands the trace over and forgets the match.
/// </summary>
public sealed class MatchTraceRecorder(IMatchRepository matches) : IDomainEventListener
{
    // Concurrent on the outside, because a batch plays its matches at the same time (ADR 0030) and this
    // listens to every one of them; and guarded on the inside, because a match being played is also a match
    // being read. A match has one writer -- it plays its rounds in order -- but the table polls the trace of
    // a match still running (ADR 0054), and enumerating a List<T> another thread is appending to throws.
    private readonly ConcurrentDictionary<MatchId, Recorded> _entries = new();

    public Type EventType => typeof(IMatchEvent);

    public async Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent is not IMatchEvent matchEvent)
        {
            return;
        }

        var match = await matches.FindAsync(matchEvent.MatchId, cancellationToken)
            ?? throw new InvalidOperationException($"Match {matchEvent.MatchId} raised an event but is not stored.");

        _entries.GetOrAdd(match.Id, _ => new Recorded()).Add(
            matchEvent,
            PlayerBoardStateProjection.Build(match, PlayerSlot.Player1),
            PlayerBoardStateProjection.Build(match, PlayerSlot.Player2));
    }

    /// <summary>
    /// The entries recorded so far for a match, as a copy; empty when none was. A copy because the match may
    /// still be playing while this is read, and what a caller walks has to be a list that stops changing.
    /// </summary>
    public IReadOnlyList<TraceEntry> EntriesOf(MatchId matchId) => _entries.GetValueOrDefault(matchId)?.Snapshot() ?? [];

    /// <summary>The trace of a match, which the recorder then forgets.</summary>
    public MatchTrace Complete(MatchId matchId, RunStamp stamp, int? seed)
    {
        ArgumentNullException.ThrowIfNull(stamp);

        _entries.TryRemove(matchId, out var recorded);
        var entries = recorded?.Snapshot() ?? [];
        return new MatchTrace
        {
            MatchId = matchId,
            Seed = seed,
            Stamp = stamp,
            Entries = entries,
            Outcome = entries.Count == 0 ? null : entries[^1].Player1.Outcome,
        };
    }

    /// <summary>One match's entries, and the lock that lets them be read while they are still being written.</summary>
    private sealed class Recorded
    {
        private readonly Lock _gate = new();
        private readonly List<TraceEntry> _entries = [];

        /// <summary>Appends the event with the boards after it, numbered where it landed.</summary>
        public void Add(IMatchEvent matchEvent, PlayerBoardState player1, PlayerBoardState player2)
        {
            lock (_gate)
            {
                _entries.Add(new TraceEntry
                {
                    Sequence = _entries.Count,
                    Round = player1.RoundNumber,
                    SubPhase = player1.SubPhase,
                    Event = matchEvent,
                    Player1 = player1,
                    Player2 = player2,
                });
            }
        }

        public IReadOnlyList<TraceEntry> Snapshot()
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }
}

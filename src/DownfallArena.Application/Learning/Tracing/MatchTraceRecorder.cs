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
    private readonly Dictionary<MatchId, List<TraceEntry>> _entries = [];

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

        if (!_entries.TryGetValue(match.Id, out var entries))
        {
            entries = [];
            _entries[match.Id] = entries;
        }

        var player1 = PlayerBoardStateProjection.Build(match, PlayerSlot.Player1);
        entries.Add(new TraceEntry
        {
            Sequence = entries.Count,
            Round = player1.RoundNumber,
            SubPhase = player1.SubPhase,
            Event = matchEvent,
            Player1 = player1,
            Player2 = PlayerBoardStateProjection.Build(match, PlayerSlot.Player2),
        });
    }

    /// <summary>The entries recorded so far for a match; empty when none was.</summary>
    public IReadOnlyList<TraceEntry> EntriesOf(MatchId matchId) => _entries.GetValueOrDefault(matchId) ?? [];

    /// <summary>The trace of a match, which the recorder then forgets.</summary>
    public MatchTrace Complete(MatchId matchId, RunStamp stamp, int? seed)
    {
        ArgumentNullException.ThrowIfNull(stamp);

        _entries.Remove(matchId, out var entries);
        entries ??= [];
        return new MatchTrace
        {
            MatchId = matchId,
            Seed = seed,
            Stamp = stamp,
            Entries = entries,
            Outcome = entries.Count == 0 ? null : entries[^1].Player1.Outcome,
        };
    }
}

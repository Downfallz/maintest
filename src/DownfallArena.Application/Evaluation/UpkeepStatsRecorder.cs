using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches.Events;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// Feeds the start of a round into <see cref="CombatStatsRecorder"/>, so what a spell's conditions do at
/// upkeep lands on that spell's tally rather than nowhere (ADR 0027).
/// <para>
/// A listener of its own because a listener answers one event type, and the two events are raised at
/// different points of the round. It holds no state: the tally it feeds is the recorder's.
/// </para>
/// </summary>
public sealed class UpkeepStatsRecorder(CombatStatsRecorder combat) : DomainEventListener<OngoingEffectsApplied>
{
    protected override Task HandleAsync(OngoingEffectsApplied domainEvent, CancellationToken cancellationToken)
    {
        combat.Record(domainEvent);
        return Task.CompletedTask;
    }
}

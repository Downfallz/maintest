using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches.Events;

namespace DownfallArena.Infrastructure.Tests.Invariants;

/// <summary>One match of a batch: what the batch reported, and every event the match raised on the way.</summary>
public sealed record PlayedMatch(MatchResult Result, IReadOnlyList<IMatchEvent> Events)
{
    public IEnumerable<CombatActionFrame> Frames => Events.OfType<CombatActionResolved>().Select(resolved => resolved.Frame).OfType<CombatActionFrame>();

    public override string ToString() => $"seed {Result.Seed}";
}

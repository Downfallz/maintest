using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;

namespace DownfallArena.Infrastructure.Tests.Invariants;

/// <summary>
/// One match of a batch: what the batch reported, every event the match raised on the way, and when its
/// creatures chose their spells (ADR 0110).
/// </summary>
public sealed record PlayedMatch(MatchResult Result, IReadOnlyList<IMatchEvent> Events, CombatDeclaration Declaration)
{
    public IEnumerable<CombatActionFrame> Frames => Events.OfType<CombatActionResolved>().Select(resolved => resolved.Frame).OfType<CombatActionFrame>();

    public override string ToString() =>
        Declaration == CombatDeclaration.AtActivation ? $"seed {Result.Seed}" : $"seed {Result.Seed} (hidden intents)";
}

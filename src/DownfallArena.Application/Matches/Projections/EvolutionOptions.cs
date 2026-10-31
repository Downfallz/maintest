using DownfallArena.Domain.Matches.Rounds;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// The picks a player has left and, per creature that can still buy something, the packages available to it.
/// Passing is always allowed. What each package contains is in the catalogue, not here: this is the legal
/// move list, and it is the domain's answer rather than a client's own reading of the schedule.
/// </summary>
public sealed record EvolutionOptions(int RemainingPicks, IReadOnlyList<EvolutionOption> Creatures)
{
    /// <summary>
    /// The options the match would serve once this pick is in: one pick fewer, and its creature gone, since a
    /// creature buys at most one package an opportunity. A pick is face down until both players are done (ADR
    /// 0089), so it changes nothing else the player is offered. This is what lets a table ask a player all their
    /// picks before any reaches the match (ADR 0117); <c>MatchDriverTests</c> holds the two to each other.
    /// </summary>
    public EvolutionOptions After(EvolutionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        return new EvolutionOptions(RemainingPicks - 1, [.. Creatures.Where(option => option.Creature != choice.Creature)]);
    }
}

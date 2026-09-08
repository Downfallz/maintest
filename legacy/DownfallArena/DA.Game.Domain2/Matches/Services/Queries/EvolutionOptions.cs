using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Services.Queries;

// =================================================
// Planning_Evolution
// =================================================
public sealed record EvolutionOptions
{
    /// <summary>
    /// How many evolution (spell unlock) picks remain for this player.
    /// </summary>
    public required int RemainingPicks { get; init; }

    /// <summary>
    /// Creatures that are currently eligible to receive an evolution pick.
    /// If RemainingPicks > 0, this list should not be empty.
    /// </summary>
    public required IReadOnlyList<CreatureId> LegalCreatureIds { get; init; }
}

using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Services.Queries;

// =================================================
// Planning_Speed
// =================================================
public sealed record SpeedOptions
{
    /// <summary>
    /// How many speed choices are still required for this player.
    /// </summary>
    public required int Remaining { get; init; }

    /// <summary>
    /// Creatures that must still choose a speed (Quick / Standard).
    /// </summary>
    public required IReadOnlyList<CreatureId> RequiredCreatures { get; init; }
}

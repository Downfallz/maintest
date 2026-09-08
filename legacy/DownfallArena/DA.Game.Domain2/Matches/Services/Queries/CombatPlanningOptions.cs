using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Services.Queries;

// =================================================
// Combat_IntentSelection
// =================================================
public sealed record CombatPlanningOptions
{
    /// <summary>
    /// Creatures that still need to submit a combat intent.
    /// </summary>
    public required IReadOnlyList<CreatureId> MissingCreatureIds { get; init; }
}

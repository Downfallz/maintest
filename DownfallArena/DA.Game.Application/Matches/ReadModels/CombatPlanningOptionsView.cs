using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Application.Matches.ReadModels;

public sealed record CombatPlanningOptionsView
{
    public required IReadOnlyList<CreatureId> MissingCreatureIds { get; init; }
}

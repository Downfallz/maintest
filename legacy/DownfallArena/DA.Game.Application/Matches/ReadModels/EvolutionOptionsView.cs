using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Application.Matches.ReadModels;

public sealed record EvolutionOptionsView
{
    public required int RemainingPicks { get; init; }

    public required IReadOnlyList<CreatureId> LegalCreatureIds { get; init; }
}

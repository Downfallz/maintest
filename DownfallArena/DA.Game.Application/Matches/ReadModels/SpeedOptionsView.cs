using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Application.Matches.ReadModels;

public sealed record SpeedOptionsView
{
    public required int Remaining { get; init; }
    public required IReadOnlyList<CreatureId> RequiredCreatures { get; init; }
}

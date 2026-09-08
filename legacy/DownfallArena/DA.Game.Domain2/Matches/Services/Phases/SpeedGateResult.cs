using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Services.Phases;

public sealed record SpeedGateResult(
    bool CanAdvance,
    IReadOnlyList<CreatureId> Player1MissingCreatureIds,
    IReadOnlyList<CreatureId> Player2MissingCreatureIds
);
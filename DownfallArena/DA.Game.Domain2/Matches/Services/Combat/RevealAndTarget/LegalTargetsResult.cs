using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Services.Combat.RevealAndTarget;

public sealed record LegalTargetsResult(
    int MinTargets,
    int MaxTargets,
    IReadOnlyList<CreatureId> LegalTargetIds);

using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed record TargetingFailure(
    CreatureId? TargetId,
    string ErrorCode,
    string Message);

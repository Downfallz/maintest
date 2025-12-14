using DA.Game.Domain2.Matches.Entities.Conditions;
using DA.Game.Domain2.Matches.Services.Combat.Conditions;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Services.Combat;

public sealed record ConditionApplication(
    CreatureId ActorId,
    CreatureId TargetId,
    ConditionInstance Condition
);
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public interface ITargetingPolicy
{
    Result<TargetingCheckReport> EnsureCombatActionHasValidTargets(CreaturePerspective ctx, CombatActionChoice choice);
}

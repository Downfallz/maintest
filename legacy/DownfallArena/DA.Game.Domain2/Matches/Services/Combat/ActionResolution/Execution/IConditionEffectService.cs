using DA.Game.Domain2.Matches.Entities;

namespace DA.Game.Domain2.Matches.Services.Combat.ActionResolution.Execution;

public interface IConditionEffectService
{
    void ApplyCondition(ConditionApplication application, CombatCreature target);
}

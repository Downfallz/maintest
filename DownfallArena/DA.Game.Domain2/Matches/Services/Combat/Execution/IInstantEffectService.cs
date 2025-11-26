using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Services.Combat.Execution;

namespace DA.Game.Domain2.Matches.Services.Combat;

public interface IInstantEffectService
{
    void ApplyInstantEffect(InstantEffectApplication eff, CombatCreature target);
}
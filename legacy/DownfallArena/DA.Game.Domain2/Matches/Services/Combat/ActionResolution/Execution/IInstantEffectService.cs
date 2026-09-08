using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Entities;

namespace DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;

public interface IInstantEffectService
{
    void ApplyInstantEffect(InstantEffectApplication eff, CombatCreature target);
}
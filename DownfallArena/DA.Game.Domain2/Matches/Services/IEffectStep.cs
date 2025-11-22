using DA.Game.Domain2.Matches.RuleSets;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Services;

public interface IEffectStep
{
    CombatActionResult Execute(CombatActionResult acc, CombatActionChoice intent, RuleSet rules, IClock clock);
}

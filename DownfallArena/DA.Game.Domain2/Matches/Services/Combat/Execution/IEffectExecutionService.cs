using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Services.Combat.Execution;

public interface IEffectExecutionService
{
    Result ApplyCombatResult(CombatActionResult result, Match match);
}
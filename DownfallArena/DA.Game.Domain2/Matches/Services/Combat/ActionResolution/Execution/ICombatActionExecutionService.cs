using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;

public interface ICombatActionExecutionService
{
    Result ApplyCombatResult(CombatActionResult result, IReadOnlyList<CombatCreature> allCreatures);
}
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.Conditions;

public interface IConditionResolutionService
{
    Result ResolveStartOfRound(IReadOnlyList<CombatCreature> allCreatures);
}

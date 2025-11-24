using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.Resolution;

public interface ICombatActionResolutionService
{
    public Result<CombatActionResult> Resolve(CombatActionChoice intent, Match match);
}

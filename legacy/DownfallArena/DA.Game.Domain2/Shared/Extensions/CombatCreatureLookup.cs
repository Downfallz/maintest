using DA.Game.Domain2.Matches.Entities;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Shared.Extensions;

public static class CombatCreatureLookup
{
    public static Result<CombatCreature> FindCreature(
        this IReadOnlyList<CombatCreature> creatures,
        CreatureId id,
        string errorCodeIfMissing)
    {
        var creature = creatures.SingleOrDefault(x => x.Id == id);
        if (creature is null)
            return Result<CombatCreature>.InvariantFail(errorCodeIfMissing);

        return Result<CombatCreature>.Ok(creature);
    }
}
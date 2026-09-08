using DA.Game.Domain2.Matches.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;

public sealed class DamageComputationService : IDamageComputationService
{
    public int ComputeFinalDamage(int rawDamage, CreatureSnapshot defender)
    {
        ArgumentNullException.ThrowIfNull(defender);

        if (!defender.IsAlive || rawDamage <= 0)
            return 0;

        var mitigated = Math.Max(0, rawDamage - defender.TotalDefense.Value);

        return mitigated;
    }
}

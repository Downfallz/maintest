using DA.Game.Domain2.Matches.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat;

public sealed class DamageComputationService : IDamageComputationService
{
    public int ComputeFinalDamage(int rawDamage, CharacterStatus attacker, CharacterStatus defender)
    {
        ArgumentNullException.ThrowIfNull(defender);

        if (!defender.IsAlive)
            return 0;

        var mitigated = Math.Max(0, rawDamage - defender.BaseDefense.Value - defender.BonusDefense.Value);

        return mitigated;
    }
}

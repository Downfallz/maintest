using DA.Game.Domain2.Matches.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat;

public interface IDamageComputationService
{
    int ComputeFinalDamage(
        int rawDamage,
        CharacterSnapshot defender);
}

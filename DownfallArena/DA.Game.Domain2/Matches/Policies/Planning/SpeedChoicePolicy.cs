using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Policies.Planning;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Talents;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Planning;


public class SpeedChoicePolicy : ISpeedChoicePolicy
{
    public Result EnsureCreatureCanPlaySpeedChoice(CreaturePerspective creaturePerspective, SpeedChoice choice)
    {
        ArgumentNullException.ThrowIfNull(creaturePerspective);
        ArgumentNullException.ThrowIfNull(choice);

        var creature = creaturePerspective.Actor;

        if (creature.IsDead)
            return Result.Fail("D7XX_CREATURE_DEAD");

        return Result.Ok();
    }
}

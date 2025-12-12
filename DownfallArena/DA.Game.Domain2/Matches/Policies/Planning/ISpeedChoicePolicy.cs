using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
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

namespace DA.Game.Domain2.Matches.Policies.Planning;


public interface ISpeedChoicePolicy
{
    Result EnsureCreatureCanPlaySpeedChoice(CreaturePerspective creaturePerspective, SpeedChoice choice);
}

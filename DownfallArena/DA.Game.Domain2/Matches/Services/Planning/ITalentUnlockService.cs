using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Talents;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Planning;


public interface ITalentUnlockService
{
    Result<IReadOnlyList<SpellId>> GetUnlockableSpells(
        CreatureSnapshot creature,
        TalentTree? tree);

    Result ValidateSpellUnlock(CreaturePerspective creaturePerspective, TalentTree? tree, SpellUnlockChoice choice);
}

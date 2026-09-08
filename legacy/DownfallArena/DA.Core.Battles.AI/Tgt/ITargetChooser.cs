using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Teams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Core.Game.AI.Tgt
{
    public interface ITargetChooser
    {
        List<Guid> ChooseTargetForSpell(Spell spell, List<Character> aliveCharacters, List<Character> aliveEnemies);
       
    }
}

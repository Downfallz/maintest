using System;
using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.AI.CharAction.Tgt
{
    public interface ITargetChooser
    {
        List<Guid> ChooseTargetForSpell(Spell spell, List<Character> aliveCharacters, List<Character> aliveEnemies);

    }
}

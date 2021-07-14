using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using System;
using System.Collections.Generic;

namespace DA.AI.Tgt
{
    public interface ITargetChooser
    {
        List<Guid> ChooseTargetForSpell(Spell spell, List<Character> aliveCharacters, List<Character> aliveEnemies);

    }
}

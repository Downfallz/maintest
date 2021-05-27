using System;
using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.AI.Tgt
{
    public class RandomTargetChooser : ITargetChooser
    {
        public List<Guid> ChooseTargetForSpell(Spell spell, List<Character> aliveCharacters, List<Character> aliveEnemies)

        {
            var targets = new List<Guid>();
            Random rnd = new Random();
            if (spell.SpellType == SpellType.Defensive)
            {
                var possibleTargetsCount = aliveCharacters.Count;
                var spellTargetCount = spell.NbTargets;

                List<int> picked = new List<int>();
                var count = 0;
                while(count != spellTargetCount && count < possibleTargetsCount)
                {
                    var rndNumber = rnd.Next(0, possibleTargetsCount);
                    if (!picked.Contains(rndNumber))
                    {
                        targets.Add(aliveCharacters[rndNumber].Id);
                        count++;
                    }
                }
            }
            else if (spell.SpellType == SpellType.Offensive)
            {
                var possibleTargetsCount = aliveEnemies.Count;
                var spellTargetCount = spell.NbTargets;
                List<int> picked = new List<int>();
                var count = 0;
                while (count != spellTargetCount && count < possibleTargetsCount)
                {
                    var rndNumber = rnd.Next(0, possibleTargetsCount);
                    if (!picked.Contains(rndNumber))
                    {
                        targets.Add(aliveEnemies[rndNumber].Id);
                        count++;
                    }
                }
            }

            return targets;
        }
    }
}

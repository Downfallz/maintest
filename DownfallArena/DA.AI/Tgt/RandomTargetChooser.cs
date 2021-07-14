using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System;
using System.Collections.Generic;

namespace DA.AI.Tgt
{
    public class RandomTargetChooser : ITargetChooser
    {
        public List<Guid> ChooseTargetForSpell(Spell spell, List<Character> aliveCharacters, List<Character> aliveEnemies)

        {
            List<Guid> targets = new List<Guid>();
            Random rnd = new Random();
            if (spell.SpellType == SpellType.Defensive)
            {
                int possibleTargetsCount = aliveCharacters.Count;
                int? spellTargetCount = spell.NbTargets;

                List<int> picked = new List<int>();
                int count = 0;
                while (count != spellTargetCount && count < possibleTargetsCount)
                {
                    int rndNumber = rnd.Next(0, possibleTargetsCount);
                    if (!picked.Contains(rndNumber))
                    {
                        targets.Add(aliveCharacters[rndNumber].Id);
                        count++;
                    }
                }
            }
            else if (spell.SpellType == SpellType.Offensive)
            {
                int possibleTargetsCount = aliveEnemies.Count;
                int? spellTargetCount = spell.NbTargets;
                List<int> picked = new();
                int count = 0;
                while (count != spellTargetCount && count < possibleTargetsCount)
                {
                    int rndNumber = rnd.Next(0, possibleTargetsCount);
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

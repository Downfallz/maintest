using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Teams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Core.Game.AI.Tgt
{
    public class RandomTargetChooser : ITargetChooser
    {
        public List<Guid> ChooseTargetForSpell(Spell spell, List<Character> aliveCharacters, List<Character> aliveEnemies)

        {
            var targets = new List<Guid>();
            Random rnd = new Random();
            if (spell.SpellType == Domain.Base.Talents.Enum.SpellType.Defensive)
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
            else if (spell.SpellType == Domain.Base.Talents.Enum.SpellType.Offensive)
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

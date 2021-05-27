using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DA.Core.Domain.Base.Teams;
using DA.Core.Domain.Battles;
using DA.Core.Domain.Battles.Enum;

namespace DA.Core.Game.AI
{
    public class PlayChooser
    {
        public List<SpellUnlockChoice> GetRandomSpellUnlocks(List<Character> aliveCharacters)
        {
            List<SpellUnlockChoice> choices = new List<SpellUnlockChoice>();
            var rnd = new Random();
            foreach (var c in aliveCharacters)
            {
                var possibleList = c.TalentTreeStructure.Root.GetNextChildrenToUnlock();
                var possibleListCount = possibleList.Count;

                choices.Add(new SpellUnlockChoice()
                {
                    CharacterId = c.Id,
                    Spell = possibleList[rnd.Next(0, possibleListCount)].Spell
                });
            }

            return choices;
        }
    }
}

using System;
using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement;

namespace DA.AI.Spl
{
    public class RandomSpellUnlockChooser : ISpellUnlockChooser
    {
        public List<SpellUnlockChoice> GetSpellUnlockChoices(List<Character> aliveCharacters)
        {
            List<SpellUnlockChoice> choices = new List<SpellUnlockChoice>();
            Random rnd = new Random();

            int count = 0;
            foreach (Character c in aliveCharacters)
            {
                if (count == 2)
                    break;
                List<TalentNode> possibleList = c.TalentTreeStructure.Root.GetNextChildrenToUnlock();
                int possibleListCount = possibleList.Count;

                choices.Add(new SpellUnlockChoice()
                {
                    CharacterId = c.Id,
                    Spell = possibleList[rnd.Next(0, possibleListCount)].Spell
                });
                count++;
            }

            return choices;
        }
    }
}

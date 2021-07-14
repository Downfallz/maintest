using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using System;
using System.Collections.Generic;

namespace DA.AI.Spl
{
    public class RandomSpellChooser : ISpellChooser
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
                List<Game.Domain.Models.GameFlowEngine.TalentsManagement.TalentNode> possibleList = c.TalentTreeStructure.Root.GetNextChildrenToUnlock();
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

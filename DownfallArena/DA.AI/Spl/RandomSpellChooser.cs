using System;
using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;

namespace DA.AI.Spl
{
    public class RandomSpellChooser : ISpellChooser
    {
        public List<SpellUnlockChoice> GetSpellUnlockChoices(List<Character> aliveCharacters)
        {
            List<SpellUnlockChoice> choices = new List<SpellUnlockChoice>();
            var rnd = new Random();

            int count = 0;
            foreach (var c in aliveCharacters)
            {
                if (count == 2)
                    break;
                var possibleList = c.TalentTreeStructure.Root.GetNextChildrenToUnlock();
                var possibleListCount = possibleList.Count;

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

using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using System;
using System.Collections.Generic;

namespace DA.AI
{
    public class PlayChooser
    {
        public List<SpellUnlockChoice> GetRandomSpellUnlocks(List<Character> aliveCharacters)
        {
            List<SpellUnlockChoice> choices = new List<SpellUnlockChoice>();
            Random rnd = new Random();
            foreach (Character c in aliveCharacters)
            {
                List<Game.Domain.Models.GameFlowEngine.TalentsManagement.TalentNode> possibleList = c.TalentTreeStructure.Root.GetNextChildrenToUnlock();
                int possibleListCount = possibleList.Count;

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

using System;
using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement;

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
                List<TalentNode> possibleList = c.TalentTreeStructure.Root.GetNextChildrenToUnlock();
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

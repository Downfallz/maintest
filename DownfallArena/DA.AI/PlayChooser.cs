using System;
using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;

namespace DA.AI
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

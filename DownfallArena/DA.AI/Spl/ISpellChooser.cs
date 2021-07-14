using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using System.Collections.Generic;

namespace DA.AI.Spl
{
    public interface ISpellChooser
    {
        List<SpellUnlockChoice> GetSpellUnlockChoices(List<Character> aliveCharacters);
    }
}
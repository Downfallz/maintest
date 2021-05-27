using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;

namespace DA.AI.Spl
{
    public interface ISpellChooser
    {
        List<SpellUnlockChoice> GetSpellUnlockChoices(List<Character> aliveCharacters);
    }
}
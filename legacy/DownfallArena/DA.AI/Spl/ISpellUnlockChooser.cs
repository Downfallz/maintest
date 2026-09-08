using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;

namespace DA.AI.Spl
{
    public interface ISpellUnlockChooser
    {
        List<SpellUnlockChoice> GetSpellUnlockChoices(Battle battle, List<Character> aliveCharacters,
            List<Character> aliveEnemies);
    }
}
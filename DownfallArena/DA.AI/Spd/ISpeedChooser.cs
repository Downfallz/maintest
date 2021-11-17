using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;

namespace DA.AI.Spd
{
    public interface ISpeedChooser
    {
        List<SpeedChoice> GetSpeedChoices(Battle battle, List<Character> aliveCharacters, List<Character> aliveEnemies);
    }
}
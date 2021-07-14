using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using System.Collections.Generic;

namespace DA.AI.Spd
{
    public interface ISpeedChooser
    {
        List<SpeedChoice> GetSpeedChoices(Battle battle, List<Character> aliveCharacters, List<Character> aliveEnemies);
    }
}
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic.Enum;
using System;
using System.Collections.Generic;

namespace DA.AI.Spd
{
    public class RandomSpeedChooser : ISpeedChooser
    {
        public List<SpeedChoice> GetSpeedChoices(Battle battle, List<Character> aliveCharacters, List<Character> aliveEnemies)
        {
            List<SpeedChoice> choices = new List<SpeedChoice>();
            Random rnd = new Random();

            foreach (Character c in aliveCharacters)
            {
                choices.Add(new SpeedChoice()
                {
                    CharacterId = c.Id,
                    Speed = rnd.NextDouble() < 0.5 ? Speed.Quick : Speed.Standard
                });
            }

            return choices;
        }
    }
}

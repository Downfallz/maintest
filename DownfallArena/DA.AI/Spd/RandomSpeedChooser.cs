using System;
using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.CombatMechanic.Enum;

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

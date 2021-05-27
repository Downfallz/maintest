using System;
using System.Collections.Generic;
using System.Linq;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic.Enum;

namespace DA.AI.Spd
{
    public class SpeedChooser : ISpeedChooser
    {
        public List<SpeedChoice> GetSpeedChoices(Battle battle, List<Character> aliveCharacters, List<Character> aliveEnemies)
        {
            List<SpeedChoice> choices = new List<SpeedChoice>();
            var rnd = new Random();

            var friendLowOnHp = aliveCharacters.Any(x => x.Health <= 5);
            var enemiesLowOnHp = aliveEnemies.Any(x => x.Health <= 5);

            foreach (var c in aliveCharacters)
            {
                Speed speed = Speed.Standard;
                if (friendLowOnHp && c.IsAHealer())
                {
                    speed = Speed.Quick;
                }
                else if (enemiesLowOnHp)
                {
                    speed = Speed.Quick;
                }
                
                choices.Add(new SpeedChoice()
                {
                    CharacterId = c.Id,
                    Speed = speed
                });
            }

            return choices;
        }
    }
}

using System;
using System.Collections.Generic;
using DA.Core.Domain.Base.Teams;
using DA.Core.Domain.Battles;
using DA.Core.Domain.Battles.Enum;

namespace DA.Core.Game.AI.Spd
{
    public class RandomSpeedChooser : ISpeedChooser
    {
        public List<SpeedChoice> GetSpeedChoices(Battle battle, List<Character> aliveCharacters, List<Character> aliveEnemies)
        {
            List<SpeedChoice> choices = new List<SpeedChoice>();
            var rnd = new Random();

            foreach (var c in aliveCharacters)
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

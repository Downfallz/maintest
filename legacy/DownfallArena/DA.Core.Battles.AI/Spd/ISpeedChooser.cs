using System.Collections.Generic;
using DA.Core.Domain.Base.Teams;
using DA.Core.Domain.Battles;

namespace DA.Core.Game.AI.Spd
{
    public interface ISpeedChooser
    {
        List<SpeedChoice> GetSpeedChoices(Battle battle, List<Character> aliveCharacters, List<Character> aliveEnemies);
    }
}
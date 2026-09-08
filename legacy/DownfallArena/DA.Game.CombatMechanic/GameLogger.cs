using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DA.Game.Domain;

namespace DA.Game.CombatMechanic
{
    public class GameLogger : IGameLogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}

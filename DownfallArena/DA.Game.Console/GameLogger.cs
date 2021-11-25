using DA.AI;
using DA.AI.Spd;
using DA.AI.Spl;
using DA.Game;
using DA.Game.CombatMechanic.IoC;
using DA.Game.Domain.Services;
using DA.Game.IoC;
using DA.Game.Resources.IoC;
using DA.Game.TalentsManagement.IoC;
using Microsoft.Extensions.DependencyInjection;
using System;
using DA.AI.CharAction;
using DA.Game.Domain;

namespace DA.Csl
{
    public class GameLogger : IGameLogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}

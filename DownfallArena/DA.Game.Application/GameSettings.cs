using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application;
public sealed class GameSettings : IGameSettings
{
    public bool SimulationMode { get; set; }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Simulation.NewFolder;

public enum MatchEndReason
{
    Unknown = 0,
    Victory,          // Normal win condition met
    Draw,             // If you support it
    MaxStepsReached,  // Safety stop in sims
    Error             // Invariant broken, etc.
}

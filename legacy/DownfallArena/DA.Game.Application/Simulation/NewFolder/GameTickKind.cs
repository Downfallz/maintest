using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Simulation.NewFolder;
public enum GameTickKind
{
    EvolutionDecision,
    SpeedDecision,
    CombatIntent,
    RevealTargets,
    CombatResolution,
    RoundTransition,
    MatchEnd
}
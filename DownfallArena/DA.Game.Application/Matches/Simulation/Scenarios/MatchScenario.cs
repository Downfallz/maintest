using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.Simulation.Scenarios;
public sealed record MatchScenario(
    string Name,
    PlayerSimProfile Player1,
    PlayerSimProfile Player2,
    int MaxTurns = 200
);
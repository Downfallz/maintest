using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Simulation.Matches.Scenarios;
public sealed record MatchScenario(
    string Name,
    PlayerSimProfile Player1,
    PlayerSimProfile Player2,
    int MaxTurns = 200
);
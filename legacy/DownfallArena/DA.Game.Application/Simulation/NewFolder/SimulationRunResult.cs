using DA.Game.Application.Simulation.Matches.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Simulation.NewFolder;

public sealed record SimulationRunResult(
    MatchResult Result,
    IReadOnlyList<GameTick> Ticks
);

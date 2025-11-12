//using DA.Game.Application.Matches.Simulation.Results;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DA.Game.Application.Matches.Simulation.Metrics;
//public sealed class MetricsAggregator
//{
//    private readonly List<MatchResult> _results = new();
//    public void Add(MatchResult r) => _results.Add(r);
//    public SimulationSummary Summary() => new(
//        AvgTurns: _results.Average(r => r.Turns),
//        WinRateBotA: _results.Count(r => r.Winner == "BotA") / (double)_results.Count
//    );
//}
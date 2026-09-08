using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.Resolution;

public sealed record CritComputationResult
{
    public bool IsCritical { get; }
    public double ChanceUsed { get; }       // 0.0 → 1.0 chance totale de crit
    public double Roll { get; }             // RNG roll pour débogage / replay
    public double Multiplier { get; }       // ex: 2.0

    private CritComputationResult(bool isCritical, double chanceUsed, double roll, double multiplier)
    {
        IsCritical = isCritical;
        ChanceUsed = chanceUsed;
        Roll = roll;
        Multiplier = multiplier;
    }

    public static CritComputationResult Critical(double chanceUsed, double roll, double multiplier)
        => new(true, chanceUsed, roll, multiplier);

    public static CritComputationResult Normal(double chanceUsed, double roll)
        => new(false, chanceUsed, roll, 1.0);
}


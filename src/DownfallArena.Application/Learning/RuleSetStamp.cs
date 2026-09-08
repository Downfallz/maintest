using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Learning;

/// <summary>
/// The values of a rule set, as plain numbers an artifact can carry and a reader can compare.
/// </summary>
public sealed record RuleSetStamp(int TeamSize, int EnergyPerRound, int EvolutionPicksPerRound, int RoundCap, double CriticalMultiplier)
{
    public static RuleSetStamp Of(RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        return new RuleSetStamp(ruleSet.TeamSize, ruleSet.EnergyPerRound, ruleSet.EvolutionPicksPerRound, ruleSet.RoundCap, ruleSet.CriticalMultiplier);
    }
}

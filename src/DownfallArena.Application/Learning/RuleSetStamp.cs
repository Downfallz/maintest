using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Learning;

/// <summary>
/// The values of a rule set, as plain numbers an artifact can carry and a reader can compare.
/// </summary>
/// <remarks>
/// The evolution schedule is part of the stamp because identical spell values can otherwise conceal a
/// different progression: the same catalogue bought two packages a round and bought every other round is two
/// games, and two runs of them are not comparable (ADR 0056).
/// </remarks>
public sealed record RuleSetStamp(
    int TeamSize,
    int EnergyPerRound,
    int EvolutionPicksPerOpportunity,
    int RoundCap,
    double CriticalMultiplier,
    int FirstEvolutionRound,
    int EvolutionInterval)
{
    public static RuleSetStamp Of(RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        return new RuleSetStamp(
            ruleSet.TeamSize,
            ruleSet.EnergyPerRound,
            ruleSet.EvolutionPicksPerOpportunity,
            ruleSet.RoundCap,
            ruleSet.CriticalMultiplier,
            ruleSet.FirstEvolutionRound,
            ruleSet.EvolutionInterval);
    }
}

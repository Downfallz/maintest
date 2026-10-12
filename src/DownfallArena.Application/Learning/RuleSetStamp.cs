using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Learning;

/// <summary>
/// The values of a rule set, as plain numbers an artifact can carry and a reader can compare.
/// </summary>
/// <remarks>
/// The evolution schedule is part of the stamp because identical spell values can otherwise conceal a
/// different progression: the same catalogue bought two packages a round and bought every other round is two
/// games, and two runs of them are not comparable (ADR 0056). So is the combat declaration (ADR 0110): a run that
/// chooses spells at the slot is not the game of one that declares them before combat. An artifact writes it
/// only when it is not the default, so every stamp written before it reads the same as one written after.
/// </remarks>
public sealed record RuleSetStamp(
    int TeamSize,
    int EnergyPerRound,
    int EvolutionPicksPerOpportunity,
    int RoundCap,
    double CriticalMultiplier,
    int FirstEvolutionRound,
    int EvolutionInterval,
    CombatDeclaration CombatDeclaration = CombatDeclaration.HiddenIntent)
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
            ruleSet.EvolutionInterval,
            ruleSet.CombatDeclaration);
    }
}

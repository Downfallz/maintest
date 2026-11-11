using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The rule set a table plays, as its record keeps it (ADR 0163): every number and the combat declaration, so a
/// table picked up again is played under the rules it began under, whatever rules file the host now reads.
/// </summary>
internal sealed record RecordedRules(
    int TeamSize,
    int EnergyPerRound,
    int EvolutionPicksPerOpportunity,
    int RoundCap,
    double CriticalMultiplier,
    int FirstEvolutionRound,
    int EvolutionInterval,
    string CombatDeclaration)
{
    public static RecordedRules Of(RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new RecordedRules(
            rules.TeamSize,
            rules.EnergyPerRound,
            rules.EvolutionPicksPerOpportunity,
            rules.RoundCap,
            rules.CriticalMultiplier,
            rules.FirstEvolutionRound,
            rules.EvolutionInterval,
            CombatDeclarationText.Of(rules.CombatDeclaration));
    }

    /// <summary>The rule set again; a record whose rules are not playable is refused as any unreadable record is.</summary>
    public RuleSet ToRuleSet()
    {
        if (!CombatDeclarationText.TryParse(CombatDeclaration, out var declaration))
        {
            throw new InvalidDataException(CombatDeclarationText.Unknown(CombatDeclaration));
        }

        try
        {
            return RuleSet.Create(TeamSize, EnergyPerRound, EvolutionPicksPerOpportunity, RoundCap, CriticalMultiplier, FirstEvolutionRound, EvolutionInterval, declaration);
        }
        catch (ArgumentOutOfRangeException failure)
        {
            throw new InvalidDataException($"The rules this table was recorded with are not playable: {failure.Message}", failure);
        }
    }
}

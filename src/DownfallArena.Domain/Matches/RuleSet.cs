namespace DownfallArena.Domain.Matches;

/// <summary>
/// The tunable parameters of a match. Values come from configuration, never from constants in the rules.
/// </summary>
public sealed record RuleSet
{
    private RuleSet(int teamSize, int energyPerRound, int evolutionPicksPerRound, int roundCap, double criticalMultiplier)
    {
        TeamSize = teamSize;
        EnergyPerRound = energyPerRound;
        EvolutionPicksPerRound = evolutionPicksPerRound;
        RoundCap = roundCap;
        CriticalMultiplier = criticalMultiplier;
    }

    /// <summary>
    /// The prototype's values: three creatures, two energy and two evolution picks per round, thirty rounds, double damage on a crit.
    /// </summary>
    public static RuleSet Default { get; } = new(3, 2, 2, 30, 2.0);

    public int TeamSize { get; }

    public int EnergyPerRound { get; }

    public int EvolutionPicksPerRound { get; }

    /// <summary>
    /// The round after which the match ends by the health tiebreak (ADR 0011).
    /// </summary>
    public int RoundCap { get; }

    public double CriticalMultiplier { get; }

    public static RuleSet Create(int teamSize, int energyPerRound, int evolutionPicksPerRound, int roundCap, double criticalMultiplier)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(teamSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(energyPerRound);
        ArgumentOutOfRangeException.ThrowIfNegative(evolutionPicksPerRound);
        ArgumentOutOfRangeException.ThrowIfLessThan(roundCap, 1);
        if (!double.IsFinite(criticalMultiplier) || criticalMultiplier < 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(criticalMultiplier), criticalMultiplier, "The critical multiplier must be a finite number of at least 1.");
        }

        return new RuleSet(teamSize, energyPerRound, evolutionPicksPerRound, roundCap, criticalMultiplier);
    }
}
